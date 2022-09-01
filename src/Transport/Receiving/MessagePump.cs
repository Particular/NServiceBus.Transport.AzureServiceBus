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

            var fullyQualifiedNamespace = ServiceBusConnectionStringProperties.Parse(connectionString).FullyQualifiedNamespace;

            serviceBusClient = tokenCredential != null
                    ? new ServiceBusClient(fullyQualifiedNamespace, tokenCredential, serviceBusClientOptions)
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

            // no Task.Run() here because ReceiveMessagesAndSwallowExceptions immediately yields with an await
            receiveLoopTask = ReceiveMessagesAndSwallowExceptions();
        }

        async Task ReceiveMessagesAndSwallowExceptions()
        {
            try
            {
                while (!messageProcessing.IsCancellationRequested)
                {
                    await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);

                    _ = ReceiveMessagesSwallowExceptionsAndReleaseSemaphore(messageProcessing.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ReceiveMessagesSwallowExceptionsAndReleaseSemaphore(CancellationToken messageReceivingCancellationToken)
        {
            try
            {
                await ReceiveMessage(messageReceivingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(messageReceivingCancellationToken))
            {
                // private token, pump is being stopped, log the exception in case the stack trace is ever needed for debugging
                logger.Debug("Operation canceled while stopping message pump.", ex);
            }
            catch (Exception ex)
            {
                logger.Error("Error receiving messages.", ex);
            }
            finally
            {
                try
                {
                    _ = semaphore.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Can happen during endpoint shutdown
                }
            }
        }

        async Task ReceiveMessage(CancellationToken messageReceivingCancellationToken)
        {
            ServiceBusReceivedMessage message = null;

            try
            {
                message = await receiver.ReceiveMessageAsync(cancellationToken: messageReceivingCancellationToken).ConfigureAwait(false);

                circuitBreaker.Success();
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)
            {
            }
            catch (ObjectDisposedException)
            {
                // Can happen during endpoint shutdown
            }
            catch (Exception exception) when (!exception.IsCausedBy(messageReceivingCancellationToken))
            {
                logger.Warn($"Failed to receive a message. Exception: {exception.Message}", exception);

                await circuitBreaker.Failure(exception).ConfigureAwait(false);
            }

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null || messageProcessing.IsCancellationRequested)
            {
                return;
            }

            string messageId;
            Dictionary<string, string> headers;
            BinaryData body;

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.GetBody();
            }
            catch (Exception exception)
            {
                var tryDeadlettering = pushSettings.RequiredTransactionMode != TransportTransactionMode.None;

                logger.Warn($"Poison message detected. " +
                    $"Message {(tryDeadlettering ? "will be moved to the poison queue" : "will be discarded, transaction mode is set to None")}. " +
                    $"Exception: {exception.Message}", exception);

                if (tryDeadlettering)
                {
                    try
                    {
                        await receiver.DeadLetterMessageAsync(message, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message, cancellationToken: messageReceivingCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception deadLetterException) when (!deadLetterException.IsCausedBy(messageReceivingCancellationToken))
                    {
                        // nothing we can do about it, message will be retried
                        logger.Warn($"Failed to deadletter a message. Exception: {deadLetterException.Message}", deadLetterException);
                    }
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
                    contextBag.Set(receiver);
                    contextBag.GetOrCreate<NativeMessageCustomizer>();

                    var messageContext = new MessageContext(messageId, headers, body.ToArray(), transportTransaction,
                        receiveCancellationTokenSource, contextBag);

                    await onMessage(messageContext).ConfigureAwait(false);

                    if (receiveCancellationTokenSource.IsCancellationRequested == false)
                    {
                        await receiver.SafeCompleteMessageAsync(message, pushSettings.RequiredTransactionMode, transaction)
                            .ConfigureAwait(false);

                        transaction?.Commit();
                    }

                    if (receiveCancellationTokenSource.IsCancellationRequested)
                    {
                        await receiver.SafeAbandonMessageAsync(message, pushSettings.RequiredTransactionMode).ConfigureAwait(false);

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
                            await receiver.SafeCompleteMessageAsync(message, pushSettings.RequiredTransactionMode, transaction).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.SafeAbandonMessageAsync(message, pushSettings.RequiredTransactionMode).ConfigureAwait(false);
                    }
                }
                catch (ServiceBusException onErrorException) when (onErrorException.Reason == ServiceBusFailureReason.MessageLockLost || onErrorException.Reason == ServiceBusFailureReason.ServiceTimeout)
                {
                    logger.Debug("Failed to execute recoverability.", onErrorException);
                }
                catch (Exception onErrorException)
                {
                    criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorException);

                    await receiver.SafeAbandonMessageAsync(message, pushSettings.RequiredTransactionMode).ConfigureAwait(false);
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

            while (semaphore.CurrentCount != maxConcurrency)
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