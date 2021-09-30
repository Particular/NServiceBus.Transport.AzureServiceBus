namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Extensibility;
    using Logging;

    class MessagePump : IPushMessages
    {
        readonly string connectionString;
        readonly int prefetchMultiplier;
        readonly int? overriddenPrefetchCount;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly ServiceBusRetryOptions retryOptions;
        readonly ServiceBusTransportType transportType;
        readonly TokenCredential tokenCredential;
        int numberOfExecutingReceives;
        bool enableCrossEntityTransactions;
        ServiceBusClient serviceBusClient;

        // Init
        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        PushSettings pushSettings;
        CriticalError criticalError;

        // Start
        Task receiveLoopTask;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        int maxConcurrency;
        ServiceBusReceiver receiver;
        static readonly ILog logger = LogManager.GetLogger<MessagePump>();

        public MessagePump(string connectionString, TokenCredential tokenCredential, int prefetchMultiplier, int? overriddenPrefetchCount,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, ServiceBusRetryOptions retryOptions, ServiceBusTransportType transportType)
        {
            this.connectionString = connectionString;
            this.tokenCredential = tokenCredential;
            this.prefetchMultiplier = prefetchMultiplier;
            this.overriddenPrefetchCount = overriddenPrefetchCount;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.retryOptions = retryOptions;
            this.transportType = transportType;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            if (settings.PurgeOnStartup)
            {
                throw new Exception("Azure Service Bus transport doesn't support PurgeOnStartup behavior");
            }

            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
            pushSettings = settings;

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{settings.InputQueue}'", timeToWaitBeforeTriggeringCircuitBreaker, criticalError);

            return Task.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maxConcurrency = limitations.MaxConcurrency;

            var prefetchCount = maxConcurrency * prefetchMultiplier;

            if (overriddenPrefetchCount.HasValue)
            {
                prefetchCount = overriddenPrefetchCount.Value;
            }

            enableCrossEntityTransactions = pushSettings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive;

            var serviceBusClientOptions = new ServiceBusClientOptions()
            {
                TransportType = transportType,
                EnableCrossEntityTransactions = enableCrossEntityTransactions
            };

            if (retryOptions != null)
            {
                serviceBusClientOptions.RetryOptions = retryOptions;
            }

            serviceBusClient = tokenCredential != null
                    ? new ServiceBusClient(connectionString, tokenCredential, serviceBusClientOptions)
                    : new ServiceBusClient(connectionString, serviceBusClientOptions);

            var receiveOptions = new ServiceBusReceiverOptions()
            {
                PrefetchCount = prefetchCount,
                ReceiveMode = pushSettings.RequiredTransactionMode == TransportTransactionMode.None
                    ? ServiceBusReceiveMode.ReceiveAndDelete
                    : ServiceBusReceiveMode.PeekLock
            };

            receiver = serviceBusClient.CreateReceiver(pushSettings.InputQueue, receiveOptions);

            semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            messageProcessing = new CancellationTokenSource();

            receiveLoopTask = Task.Run(() => ReceiveLoop());
        }

        async Task ReceiveLoop()
        {
            try
            {
                while (!messageProcessing.IsCancellationRequested)
                {
                    await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);

                    var receiveTask = receiver.ReceiveMessageAsync();

                    _ = ProcessMessage(receiveTask);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ProcessMessage(Task<ServiceBusReceivedMessage> receiveTask)
        {
            try
            {
                await InnerProcessMessage(receiveTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug($"Exception from {nameof(ProcessMessage)}: ", ex);
            }
            finally
            {
                try
                {
                    semaphore.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Can happen during endpoint shutdown
                }
            }
        }

        async Task InnerProcessMessage(Task<ServiceBusReceivedMessage> receiveTask)
        {
            ServiceBusReceivedMessage message = null;

            try
            {
                // Workaround for ASB MessageReceiver.Receive() that has a timeout and doesn't take a CancellationToken.
                // We want to track how many receives are waiting and could be ignored when endpoint is stopping.
                // TODO: remove workaround when https://github.com/Azure/azure-service-bus-dotnet/issues/439 is fixed
                Interlocked.Increment(ref numberOfExecutingReceives);
                message = await receiveTask.ConfigureAwait(false);

                circuitBreaker.Success();
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)
            {
            }
            catch (ObjectDisposedException)
            {
                // Can happen during endpoint shutdown
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to receive a message. Exception: {exception.Message}", exception);

                await circuitBreaker.Failure(exception).ConfigureAwait(false);
            }
            finally
            {
                // TODO: remove workaround when https://github.com/Azure/azure-service-bus-dotnet/issues/439 is fixed
                Interlocked.Decrement(ref numberOfExecutingReceives);
            }

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null || messageProcessing.IsCancellationRequested)
            {
                return;
            }

            //TODO: locktoken
            //var lockToken = message.SystemProperties.LockToken;

            string messageId;
            Dictionary<string, string> headers;
            BinaryData body;

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.Body;
            }
            catch (Exception exception)
            {
                try
                {
                    await receiver.DeadLetterMessageAsync(message, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // nothing we can do about it, message will be retried
                }

                return;
            }

            try
            {
                using (var receiveCancellationTokenSource = new CancellationTokenSource())
                using (var transaction = CreateTransaction())
                {
                    var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                    var contextBag = new ContextBag();
                    contextBag.Set(message);
                    contextBag.GetOrCreate<NativeMessageCustomizer>();

                    var messageContext = new MessageContext(messageId, headers, body.ToArray(), transportTransaction,
                        receiveCancellationTokenSource, contextBag);

                    await onMessage(messageContext).ConfigureAwait(false);

                    if (receiveCancellationTokenSource.IsCancellationRequested == false)
                    {
                        //TODO: lock token
                        await receiver.CompleteMessageAsync(message/*, lockToken */)
                            .ConfigureAwait(false);

                        transaction?.Commit();
                    }

                    if (receiveCancellationTokenSource.IsCancellationRequested)
                    {
                        //TODO: lock token
                        await receiver.AbandonMessageAsync(message/*, lockToken*/).ConfigureAwait(false);

                        transaction?.Rollback();
                    }
                }
            }
            catch (Exception exception)
            {
                try
                {
                    ErrorHandleResult result;

                    using (var transaction = CreateTransaction())
                    {
                        var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                        var errorContext = new ErrorContext(exception, message.GetNServiceBusHeaders(), messageId, body.ToArray(), transportTransaction, message.DeliveryCount);

                        result = await onError(errorContext).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            //TODO: lock token
                            await receiver.CompleteMessageAsync(message/*, lockToken*/).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        //TODO: lock token
                        await receiver.AbandonMessageAsync(message/*, lockToken*/).ConfigureAwait(false);
                    }
                }
                catch (ServiceBusException onErrorException) when (onErrorException.Reason == ServiceBusFailureReason.MessageLockLost || onErrorException.Reason == ServiceBusFailureReason.ServiceTimeout)
                {
                    logger.Debug("Failed to execute recoverability.", onErrorException);
                }
                catch (Exception onErrorException)
                {
                    criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorException);

                    //TODO: lock token
                    await receiver.AbandonMessageAsync(message/*, lockToken*/).ConfigureAwait(false);
                }
            }
        }

        CommittableTransaction CreateTransaction()
        {
            return pushSettings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
                ? new CommittableTransaction(new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable,
                    Timeout = TransactionManager.MaximumTimeout
                })
                : null;
        }

        TransportTransaction CreateTransportTransaction(string incomingQueuePartitionKey, CommittableTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();

            if (pushSettings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive)
            {
                transportTransaction.Set(serviceBusClient);
                transportTransaction.Set("IncomingQueue.PartitionKey", incomingQueuePartitionKey);
                transportTransaction.Set(transaction);
            }

            return transportTransaction;
        }

        public async Task Stop()
        {
            messageProcessing.Cancel();

            await receiveLoopTask.ConfigureAwait(false);

            while (semaphore.CurrentCount + Volatile.Read(ref numberOfExecutingReceives) != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            await receiver.CloseAsync().ConfigureAwait(false);

            semaphore?.Dispose();
            messageProcessing?.Dispose();
            circuitBreaker?.Dispose();
        }
    }
}