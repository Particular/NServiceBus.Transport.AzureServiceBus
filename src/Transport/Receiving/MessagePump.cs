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
    using IMessageReceiver = IMessageReceiver;

    class MessagePump : IMessageReceiver
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ReceiveSettings receiveSettings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        int numberOfExecutingReceives;

        OnMessage onMessage;
        OnError onError;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        // Start
        Task messageReceivingTask;
        SemaphoreSlim semaphore;
        CancellationTokenSource messageReceivingCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        int maxConcurrency;
        MessageReceiver receiver;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();

        PushRuntimeSettings limitations;

        public MessagePump(
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            AzureServiceBusTransport transportSettings,
            ReceiveSettings receiveSettings,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            NamespacePermissions namespacePermissions)
        {
            Id = receiveSettings.Id;
            this.connectionStringBuilder = connectionStringBuilder;
            this.transportSettings = transportSettings;
            this.receiveSettings = receiveSettings;
            this.criticalErrorAction = criticalErrorAction;

            if (receiveSettings.UsePublishSubscribe)
            {
                Subscriptions = new SubscriptionManager(
                    receiveSettings.ReceiveAddress,
                    transportSettings,
                    connectionStringBuilder,
                    namespacePermissions);
            }
        }

        public async Task Initialize(
            PushRuntimeSettings limitations,
            OnMessage onMessage,
            OnError onError,
            CancellationToken cancellationToken = default)
        {
            if (receiveSettings.PurgeOnStartup)
            {
                throw new Exception("Azure Service Bus transport doesn't support PurgeOnStartup behavior");
            }

            if (Subscriptions is SubscriptionManager subscriptionManager)
            {
                await subscriptionManager.CreateSubscription(cancellationToken).ConfigureAwait(false);
            }

            this.limitations = limitations;
            this.onMessage = onMessage;
            this.onError = onError;
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            maxConcurrency = limitations.MaxConcurrency;

            var prefetchCount = maxConcurrency * transportSettings.PrefetchMultiplier;

            if (transportSettings.PrefetchCount.HasValue)
            {
                prefetchCount = transportSettings.PrefetchCount.Value;
            }

            var receiveMode = transportSettings.TransportTransactionMode == TransportTransactionMode.None ? ReceiveMode.ReceiveAndDelete : ReceiveMode.PeekLock;

            if (transportSettings.TokenProvider == null)
            {
                receiver = new MessageReceiver(connectionStringBuilder.GetNamespaceConnectionString(), receiveSettings.ReceiveAddress, receiveMode, retryPolicy: transportSettings.RetryPolicy, prefetchCount);
            }
            else
            {
                receiver = new MessageReceiver(connectionStringBuilder.Endpoint, receiveSettings.ReceiveAddress, transportSettings.TokenProvider, connectionStringBuilder.TransportType, receiveMode, retryPolicy: transportSettings.RetryPolicy, prefetchCount);
            }

            semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            messageReceivingCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'", transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, ex => criticalErrorAction("Failed to receive message from Azure Service Bus.", ex, messageProcessingCancellationTokenSource.Token));

            messageReceivingTask = Task.Run(() => ReceiveMessages(messageReceivingCancellationTokenSource.Token), cancellationToken);

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            messageReceivingCancellationTokenSource?.Cancel();

            using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
            {
                await messageReceivingTask.ConfigureAwait(false);

                while (semaphore.CurrentCount + Volatile.Read(ref numberOfExecutingReceives) != maxConcurrency)
                {
                    // Do not forward cancellationToken here so that pump has ability to exit gracefully
                    // Individual message processing pipelines will be cancelled instead
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }

                await receiver.CloseAsync().ConfigureAwait(false);
            }

            semaphore?.Dispose();
            messageReceivingCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource?.Dispose();
            circuitBreaker?.Dispose();
        }

        async Task ReceiveMessages(CancellationToken messageReceivingCancellationToken)
        {
            try
            {
                while (!messageReceivingCancellationToken.IsCancellationRequested)
                {
                    await semaphore.WaitAsync(messageReceivingCancellationToken).ConfigureAwait(false);

                    var receiveTask = receiver.ReceiveAsync();

                    _ = ReceiveMessage(receiveTask, messageReceivingCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ReceiveMessage(Task<Message> receiveTask, CancellationToken messageReceivingCancellationToken)
        {
            try
            {
                await InnerReceiveMessage(receiveTask, messageReceivingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Exception from {nameof(ReceiveMessage)}: ", ex);
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

        async Task InnerReceiveMessage(Task<Message> receiveTask, CancellationToken messageReceivingCancellationToken)
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
                Logger.Warn($"Failed to receive a message. Exception: {exception.Message}", exception);

                await circuitBreaker.Failure(exception, messageReceivingCancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // TODO: remove workaround when https://github.com/Azure/azure-service-bus-dotnet/issues/439 is fixed
                Interlocked.Decrement(ref numberOfExecutingReceives);
            }

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null || messageReceivingCancellationToken.IsCancellationRequested)
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
                    await receiver.SafeDeadLetterAsync(transportSettings.TransportTransactionMode, lockToken, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message, cancellationToken: messageReceivingCancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // nothing we can do about it, message will be retried
                }

                return;
            }

            await ProcessMessage(message, lockToken, messageId, headers, body, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
        }

        async Task ProcessMessage(Message message, string lockToken, string messageId, Dictionary<string, string> headers, byte[] body, CancellationToken messageProcessingCancellationToken)
        {
            var contextBag = new ContextBag();

            try
            {
                using (var transaction = CreateTransaction())
                {
                    var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                    contextBag.Set(message);

                    var messageContext = new MessageContext(messageId, headers, body, transportTransaction, contextBag);

                    await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);

                    await receiver.SafeCompleteAsync(transportSettings.TransportTransactionMode, lockToken, transaction, messageProcessingCancellationToken)
                        .ConfigureAwait(false);

                    transaction?.Commit();
                }
            }
            catch (OperationCanceledException) when (messageProcessingCancellationToken.IsCancellationRequested)
            {
                // Shut down gracefully
            }
            catch (Exception exception)
            {
                try
                {
                    ErrorHandleResult result;

                    using (var transaction = CreateTransaction())
                    {
                        var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                        var errorContext = new ErrorContext(exception, message.GetNServiceBusHeaders(), messageId, body, transportTransaction, message.SystemProperties.DeliveryCount, contextBag);

                        result = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await receiver.SafeCompleteAsync(transportSettings.TransportTransactionMode, lockToken, transaction, messageProcessingCancellationToken).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.SafeAbandonAsync(transportSettings.TransportTransactionMode, lockToken, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (messageProcessingCancellationToken.IsCancellationRequested)
                {
                    // Shut down gracefully
                }
                catch (Exception onErrorException) when (onErrorException is MessageLockLostException || onErrorException is ServiceBusTimeoutException)
                {
                    Logger.Debug("Failed to execute recoverability.", onErrorException);
                }
                catch (Exception onErrorException)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorException, messageProcessingCancellationToken);

                    await receiver.SafeAbandonAsync(transportSettings.TransportTransactionMode, lockToken, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
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