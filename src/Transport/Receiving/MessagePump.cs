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
    using Unicast.Messages;
    using IMessageReceiver = Transport.IMessageReceiver;

    class MessagePump : IMessageReceiver
    {
        private readonly AzureServiceBusTransport transportSettings;
        private readonly ReceiveSettings receiveSettings;
        private readonly Action<string, Exception> criticalErrorAction;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        int numberOfExecutingReceives;

        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        // Start
        Task receiveLoopTask;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;
        int maxConcurrency;
        MessageReceiver receiver;

        static readonly ILog logger = LogManager.GetLogger<MessagePump>();
        
        private PushRuntimeSettings limitations;

        public MessagePump(ServiceBusConnectionStringBuilder connectionStringBuilder,
            AzureServiceBusTransport transportSettings, ReceiveSettings receiveSettings,
            Action<string, Exception> criticalErrorAction)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.transportSettings = transportSettings;
            this.receiveSettings = receiveSettings;
            this.criticalErrorAction = criticalErrorAction;
        }

        public Task Initialize(PushRuntimeSettings limitations, Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, IReadOnlyCollection<MessageMetadata> events,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (receiveSettings.PurgeOnStartup)
            {
                throw new Exception("Azure Service Bus transport doesn't support PurgeOnStartup behavior");
            }

            this.limitations = limitations;
            this.onMessage = onMessage;
            this.onError = onError;

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'", transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, criticalErrorAction);

            return Task.CompletedTask;
        }

        public Task StartReceive(CancellationToken cancellationToken = new CancellationToken())
        {
            maxConcurrency = limitations.MaxConcurrency;

            var prefetchCount = maxConcurrency * transportSettings.PrefetchMultiplier;

            if (transportSettings.PrefetchCount.HasValue)
            {
                prefetchCount = transportSettings.PrefetchCount.Value;
            }

            var receiveMode = transportSettings.TransportTransactionMode == TransportTransactionMode.None ? ReceiveMode.ReceiveAndDelete : ReceiveMode.PeekLock;

            if (transportSettings.CustomTokenProvider == null)
            {
                receiver = new MessageReceiver(connectionStringBuilder.GetNamespaceConnectionString(), receiveSettings.ReceiveAddress, receiveMode, retryPolicy: transportSettings.CustomRetryPolicy, prefetchCount);
            }
            else
            {
                receiver = new MessageReceiver(connectionStringBuilder.Endpoint, receiveSettings.ReceiveAddress, transportSettings.CustomTokenProvider, connectionStringBuilder.TransportType, receiveMode, retryPolicy: transportSettings.CustomRetryPolicy, prefetchCount);
            }

            semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            messageProcessing = new CancellationTokenSource();

            receiveLoopTask = Task.Run(() => ReceiveLoop());

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = new CancellationToken())
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
                    await receiver.SafeDeadLetterAsync(transportSettings.TransportTransactionMode, lockToken, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message).ConfigureAwait(false);
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
                        await receiver.SafeCompleteAsync(transportSettings.TransportTransactionMode, lockToken, transaction)
                            .ConfigureAwait(false);

                        transaction?.Commit();
                    }

                    if (receiveCancellationTokenSource.IsCancellationRequested)
                    {
                        await receiver.SafeAbandonAsync(transportSettings.TransportTransactionMode, lockToken).ConfigureAwait(false);

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
                            await receiver.SafeCompleteAsync(transportSettings.TransportTransactionMode, lockToken, transaction).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.SafeAbandonAsync(transportSettings.TransportTransactionMode, lockToken).ConfigureAwait(false);
                    }
                }
                catch (Exception onErrorException) when (onErrorException is MessageLockLostException || onErrorException is ServiceBusTimeoutException)
                {
                    logger.Debug("Failed to execute recoverability.", onErrorException);
                }
                catch (Exception onErrorException)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorException);

                    await receiver.SafeAbandonAsync(transportSettings.TransportTransactionMode, lockToken).ConfigureAwait(false);
                }
            }
        }

        CommittableTransaction CreateTransaction()
        {
            return transportSettings.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
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

            if (transportSettings.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive)
            {
                transportTransaction.Set((receiver.ServiceBusConnection, receiver.Path));
                transportTransaction.Set("IncomingQueue.PartitionKey", incomingQueuePartitionKey);
                transportTransaction.Set(transaction);
            }

            return transportTransaction;
        }

        public ISubscriptionManager Subscriptions { get; }

        public string Id { get; }
    }
}