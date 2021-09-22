namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Extensibility;
    using Logging;
    using IMessageReceiver = IMessageReceiver;

    class MessagePump : IMessageReceiver
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ReceiveSettings receiveSettings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly string connectionString;
        readonly ServiceBusClientOptions serviceBusClientOptions;
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
        ServiceBusReceiver receiver;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();

        PushRuntimeSettings limitations;

        public MessagePump(
            string connectionString,
            ServiceBusClientOptions serviceBusClientOptions,
            AzureServiceBusTransport transportSettings,
            ReceiveSettings receiveSettings,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            NamespacePermissions namespacePermissions)
        {
            Id = receiveSettings.Id;
            this.connectionString = connectionString;
            this.serviceBusClientOptions = serviceBusClientOptions;
            this.transportSettings = transportSettings;
            this.receiveSettings = receiveSettings;
            this.criticalErrorAction = criticalErrorAction;

            if (receiveSettings.UsePublishSubscribe)
            {
                Subscriptions = new SubscriptionManager(
                    receiveSettings.ReceiveAddress,
                    transportSettings,
                    connectionString,
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

            var receiveOptions = new ServiceBusReceiverOptions()
            {
                PrefetchCount = prefetchCount,
                //SubQueue = receiveSettings.ReceiveAddress,
                ReceiveMode = transportSettings.TransportTransactionMode == TransportTransactionMode.None
                    ? ServiceBusReceiveMode.ReceiveAndDelete
                    : ServiceBusReceiveMode.PeekLock
            };
            ServiceBusClient client = null;

            if (transportSettings.TokenCredential == null)
            {
                client = new ServiceBusClient(connectionString, serviceBusClientOptions);
            }
            else
            {
                client = new ServiceBusClient(connectionString, transportSettings.TokenCredential, serviceBusClientOptions);
            }

            receiver = client.CreateReceiver(receiveSettings.ReceiveAddress, receiveOptions);

            semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            messageReceivingCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'", transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, ex => criticalErrorAction("Failed to receive message from Azure Service Bus.", ex, messageProcessingCancellationTokenSource.Token));

            // no Task.Run() here because ReceiveMessagesAndSwallowExceptions immediately yields with an await
            messageReceivingTask = ReceiveMessagesAndSwallowExceptions(messageReceivingCancellationTokenSource.Token);

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
                    // Individual message processing pipelines will be canceled instead
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }

                try
                {
                    await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
                {
                    //anything to log here?
                    Logger.Debug($"Operation canceled while stopping the receiver {receiver.EntityPath}.", ex);
                }
            }

            semaphore?.Dispose();
            messageReceivingCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource?.Dispose();
            circuitBreaker?.Dispose();
        }

        async Task ReceiveMessagesAndSwallowExceptions(CancellationToken messageReceivingCancellationToken)
        {
            while (!messageReceivingCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await semaphore.WaitAsync(messageReceivingCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.IsCausedBy(messageReceivingCancellationToken))
                {
                    // private token, pump is being stopped, don't log exception because WaitAsync stack trace is not useful
                    break;
                }

                // no Task.Run() here to avoid a closure
                _ = ReceiveMessagesSwallowExceptionsAndReleaseSemaphore(messageReceivingCancellationToken);
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
                Logger.Debug("Operation canceled while stopping message pump.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving messages.", ex);
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
                // Workaround for ASB MessageReceiver.Receive() that has a timeout and doesn't take a CancellationToken.
                // We want to track how many receives are waiting and could be ignored when endpoint is stopping.
                // TODO: remove workaround when https://github.com/Azure/azure-service-bus-dotnet/issues/439 is fixed
                _ = Interlocked.Increment(ref numberOfExecutingReceives);
                message = await receiver.ReceiveMessageAsync(cancellationToken: messageReceivingCancellationToken).ConfigureAwait(false);

                circuitBreaker.Success();
            }
            catch (ServiceBusException ex) when (ex.IsTransient)
            {
            }
            catch (ObjectDisposedException)
            {
                // Can happen during endpoint shutdown
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageReceivingCancellationToken))
            {
                Logger.Warn($"Failed to receive a message. Exception: {ex.Message}", ex);

                await circuitBreaker.Failure(ex, messageReceivingCancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // TODO: remove workaround when https://github.com/Azure/azure-service-bus-dotnet/issues/439 is fixed
                _ = Interlocked.Decrement(ref numberOfExecutingReceives);
            }

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null)
            {
                return;
            }

            messageReceivingCancellationToken.ThrowIfCancellationRequested();

            //TODO: locktoken
            //var lockToken = message.LockToken;

            string messageId;
            Dictionary<string, string> headers;
            BinaryData body;

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.Body;
            }
            catch (Exception ex)
            {
                try
                {
                    await receiver.DeadLetterMessageAsync(message, deadLetterReason: "Poisoned message", deadLetterErrorDescription: ex.Message, cancellationToken: messageReceivingCancellationToken).ConfigureAwait(false);
                }
                catch (Exception deadLetterEx) when (!deadLetterEx.IsCausedBy(messageReceivingCancellationToken))
                {
                    // nothing we can do about it, message will be retried
                    Logger.Debug("Error dead lettering poisoned message.", deadLetterEx);
                }

                return;
            }

            // need to catch OCE here because we are switching token
            try
            {
                await ProcessMessage(message, messageId, headers, body, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationTokenSource.Token))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
        }

        async Task ProcessMessage(ServiceBusReceivedMessage message, string messageId, Dictionary<string, string> headers, BinaryData body, CancellationToken messageProcessingCancellationToken)
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

                    await receiver.CompleteMessageAsync(message, messageProcessingCancellationToken).ConfigureAwait(false);

                    transaction?.Commit();
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                try
                {
                    ErrorHandleResult result;

                    using (var transaction = CreateTransaction())
                    {
                        var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                        var errorContext = new ErrorContext(ex, message.GetNServiceBusHeaders(), messageId, body, transportTransaction, message.DeliveryCount, contextBag);

                        result = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await receiver.CompleteMessageAsync(message, messageProcessingCancellationToken).ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.AbandonMessageAsync(message, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
                    }
                }
                catch (ServiceBusException onErrorEx) when (onErrorEx.Reason == ServiceBusFailureReason.MessageLockLost || onErrorEx.Reason == ServiceBusFailureReason.ServiceTimeout)
                {
                    Logger.Debug("Failed to execute recoverability.", onErrorEx);
                }
                catch (Exception onErrorEx) when (onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                {
                    throw;
                }
                catch (Exception onErrorEx)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorEx, messageProcessingCancellationToken);

                    await receiver.AbandonMessageAsync(message, cancellationToken: messageProcessingCancellationToken).ConfigureAwait(false);
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
                transportTransaction.Set((connectionString, receiver.EntityPath));
                transportTransaction.Set("IncomingQueue.PartitionKey", incomingQueuePartitionKey);
                transportTransaction.Set(transaction);
            }

            return transportTransaction;
        }

        public ISubscriptionManager Subscriptions { get; }

        public string Id { get; }
    }
}