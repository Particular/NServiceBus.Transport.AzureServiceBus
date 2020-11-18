namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Logging;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Azure.ServiceBus.Primitives;

    class MessagePump : IPushMessages
    {
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly int prefetchMultiplier;
        readonly int? overriddenPrefetchCount;
        readonly TimeSpan timeToWaitBeforeTriggeringCircuitBreaker;
        readonly RetryPolicy retryPolicy;
        readonly ITokenProvider tokenProvider;
        int numberOfExecutingReceives;

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
        MessageReceiver receiver;

        static readonly ILog logger = LogManager.GetLogger<MessagePump>();

        public MessagePump(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider, int prefetchMultiplier, int? overriddenPrefetchCount,
            TimeSpan timeToWaitBeforeTriggeringCircuitBreaker, RetryPolicy retryPolicy)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
            this.prefetchMultiplier = prefetchMultiplier;
            this.overriddenPrefetchCount = overriddenPrefetchCount;
            this.timeToWaitBeforeTriggeringCircuitBreaker = timeToWaitBeforeTriggeringCircuitBreaker;
            this.retryPolicy = retryPolicy;
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

            var receiveMode = pushSettings.RequiredTransactionMode == TransportTransactionMode.None ? ReceiveMode.ReceiveAndDelete : ReceiveMode.PeekLock;

            if (tokenProvider == null)
            {
                receiver = new MessageReceiver(connectionStringBuilder.GetNamespaceConnectionString(), pushSettings.InputQueue, receiveMode, retryPolicy: retryPolicy, prefetchCount);
            }
            else
            {
                receiver = new MessageReceiver(connectionStringBuilder.Endpoint, pushSettings.InputQueue, tokenProvider, connectionStringBuilder.TransportType, receiveMode, retryPolicy: retryPolicy, prefetchCount);
            }

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

                    var receiveTask = receiver.ReceiveAsync();

                    ProcessMessage(receiveTask).Ignore();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ProcessMessage(Task<Message> receiveTask)
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

        async Task InnerProcessMessage(Task<Message> receiveTask)
        {
            Message message = null;

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
                logger.WarnFormat("Failed to receive a message. Exception: {0}", exception.Message);

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

            var lockToken = message.SystemProperties.LockToken;

            string messageId;
            Dictionary<string, string> headers;
            byte[] body;

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.GetBody();
            }
            catch (Exception exception)
            {
                try
                {
                    await receiver.SafeDeadLetterAsync(pushSettings.RequiredTransactionMode, lockToken, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message).ConfigureAwait(false);
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

                    var messageContext = new MessageContext(messageId, headers, body, transportTransaction,
                        receiveCancellationTokenSource, contextBag);

                    await onMessage(messageContext).ConfigureAwait(false);

                    if (receiveCancellationTokenSource.IsCancellationRequested == false)
                    {
                        await receiver.SafeCompleteAsync(pushSettings.RequiredTransactionMode, lockToken, transaction)
                            .ConfigureAwait(false);

                        transaction?.Commit();
                    }

                    if (receiveCancellationTokenSource.IsCancellationRequested)
                    {
                        await receiver.SafeAbandonAsync(pushSettings.RequiredTransactionMode, lockToken).ConfigureAwait(false);

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

                        var errorContext = new ErrorContext(exception, message.GetNServiceBusHeaders(), messageId, body, transportTransaction, message.SystemProperties.DeliveryCount);

                        result = await onError(errorContext).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await receiver.SafeCompleteAsync(pushSettings.RequiredTransactionMode, lockToken, transaction).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.SafeAbandonAsync(pushSettings.RequiredTransactionMode, lockToken).ConfigureAwait(false);
                    }
                }
                catch (Exception onErrorException) when (onErrorException is MessageLockLostException || onErrorException is ServiceBusTimeoutException)
                {
                    logger.Debug("Failed to execute recoverability.", onErrorException);
                }
                catch (Exception onErrorException)
                {
                    criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorException);

                    await receiver.SafeAbandonAsync(pushSettings.RequiredTransactionMode, lockToken).ConfigureAwait(false);
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
                transportTransaction.Set((receiver.ServiceBusConnection, receiver.Path));
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