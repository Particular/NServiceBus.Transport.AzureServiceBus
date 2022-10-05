namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Extensibility;
    using Logging;

    class MessagePump : IMessageReceiver
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ReceiveSettings receiveSettings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly ServiceBusClient serviceBusClient;

        OnMessage onMessage;
        OnError onError;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        // Start
        CancellationTokenSource messageProcessingCancellationTokenSource;
        int maxConcurrency;
        ServiceBusProcessor processor;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();

        PushRuntimeSettings limitations;

        public MessagePump(
            ServiceBusClient serviceBusClient,
            ServiceBusAdministrationClient administrativeClient,
            AzureServiceBusTransport transportSettings,
            string receiveAddress,
            ReceiveSettings receiveSettings,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            NamespacePermissions namespacePermissions)
        {
            Id = receiveSettings.Id;
            ReceiveAddress = receiveAddress;
            this.serviceBusClient = serviceBusClient;
            this.transportSettings = transportSettings;
            this.receiveSettings = receiveSettings;
            this.criticalErrorAction = criticalErrorAction;

            if (receiveSettings.UsePublishSubscribe)
            {
                Subscriptions = new SubscriptionManager(
                    ReceiveAddress,
                    transportSettings,
                    administrativeClient,
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

        public async Task StartReceive(CancellationToken cancellationToken = default)
        {
            maxConcurrency = limitations.MaxConcurrency;

            var prefetchCount = maxConcurrency * transportSettings.PrefetchMultiplier;

            if (transportSettings.PrefetchCount.HasValue)
            {
                prefetchCount = transportSettings.PrefetchCount.Value;
            }

            var receiveOptions = new ServiceBusProcessorOptions
            {
                PrefetchCount = prefetchCount,
                ReceiveMode = transportSettings.TransportTransactionMode == TransportTransactionMode.None
                    ? ServiceBusReceiveMode.ReceiveAndDelete
                    : ServiceBusReceiveMode.PeekLock,
                Identifier = Id,
                MaxConcurrentCalls = maxConcurrency,
                AutoCompleteMessages = false
            };

            processor = serviceBusClient.CreateProcessor(ReceiveAddress, receiveOptions);
            processor.ProcessErrorAsync += OnProcessorError;
            processor.ProcessMessageAsync += OnProcessMessage;

            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'", transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, ex => criticalErrorAction("Failed to receive message from Azure Service Bus.", ex, messageProcessingCancellationTokenSource.Token));

            await processor.StartProcessingAsync(cancellationToken)
                .ConfigureAwait(false);
        }

#pragma warning disable PS0018
        async Task OnProcessMessage(ProcessMessageEventArgs arg)
#pragma warning restore PS0018
        {
            string messageId;
            Dictionary<string, string> headers;
            BinaryData body;
            var message = arg.Message;

            circuitBreaker.Success();

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.GetBody();
            }
            catch (Exception ex)
            {
                var tryDeadlettering = transportSettings.TransportTransactionMode != TransportTransactionMode.None;

                Logger.Warn($"Poison message detected. Message {(tryDeadlettering ? "will be moved to the poison queue" : "will be discarded, transaction mode is set to None")}. Exception: {ex.Message}", ex);

                if (tryDeadlettering)
                {
                    try
                    {
                        await arg.DeadLetterMessageAsync(message,
                                deadLetterReason: "Poisoned message",
                                deadLetterErrorDescription: ex.Message,
                                cancellationToken: arg.CancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception deadLetterEx) when (!deadLetterEx.IsCausedBy(arg.CancellationToken))
                    {
                        // nothing we can do about it, message will be retried
                        Logger.Debug("Error dead lettering poisoned message.", deadLetterEx);
                    }
                }

                return;
            }

            // need to catch OCE here because we are switching token
            try
            {
                await ProcessMessage(message, arg, messageId, headers, body, messageProcessingCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationTokenSource.Token))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
        }

#pragma warning disable PS0018
        async Task OnProcessorError(ProcessErrorEventArgs processErrorEventArgs)
#pragma warning restore PS0018
        {
            string message = $"Failed to receive a message on pump '{processErrorEventArgs.Identifier}' listening on '{processErrorEventArgs.EntityPath}' connected to '{processErrorEventArgs.FullyQualifiedNamespace}' due to '{processErrorEventArgs.ErrorSource}'. Exception: {processErrorEventArgs.Exception}";
            // Making sure transient exceptions do not trigger the circuit breaker.
            if (processErrorEventArgs.Exception is ServiceBusException { IsTransient: true })
            {
                Logger.Debug(message, processErrorEventArgs.Exception);
                return;
            }

            Logger.Warn(message, processErrorEventArgs.Exception);
            await circuitBreaker.Failure(processErrorEventArgs.Exception, processErrorEventArgs.CancellationToken)
                .ConfigureAwait(false);
        }

        public async Task ChangeConcurrency(PushRuntimeSettings newLimitations, CancellationToken cancellationToken = default)
        {
            await StopReceive(cancellationToken).ConfigureAwait(false);
            limitations = newLimitations;
            await StartReceive(cancellationToken).ConfigureAwait(false);
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            // Wiring up the stop token to trigger the cancellation token that is being
            // used inside the message handling pipeline
            using var _ = cancellationToken
                .Register(state => (state as CancellationTokenSource)?.Cancel(),
                    messageProcessingCancellationTokenSource,
                    useSynchronizationContext: false);
            // Deliberately not passing the cancellation token forward in order to make sure
            // the processor waits until all processing handlers have returned. This makes
            // the code compliant to the previous version that uses manual receives and is aligned
            // with how the cancellation token support was initially designed.
            await processor.StopProcessingAsync(CancellationToken.None)
                .ConfigureAwait(false);

            try
            {
                await processor.CloseAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
            {
                Logger.Debug($"Operation canceled while stopping the receiver {processor.EntityPath}.", ex);
            }

            processor.ProcessErrorAsync -= OnProcessorError;
            processor.ProcessMessageAsync -= OnProcessMessage;

            await processor.DisposeAsync().ConfigureAwait(false);

            messageProcessingCancellationTokenSource?.Dispose();
            messageProcessingCancellationTokenSource = null;
            circuitBreaker?.Dispose();
        }

        async Task ProcessMessage(ServiceBusReceivedMessage message,
            ProcessMessageEventArgs processMessageEventArgs,
            string messageId, Dictionary<string, string> headers, BinaryData body,
            CancellationToken messageProcessingCancellationToken)
        {
            // args.CancellationToken is currently not used because the v8 version that supports cancellation was designed
            // to not flip the cancellation token until the very last moment in time when the stop token is flipped.
            var contextBag = new ContextBag();

            try
            {
                using (var transaction = CreateTransaction())
                {
                    var transportTransaction = CreateTransportTransaction(message.PartitionKey, transaction);

                    contextBag.Set(message);
                    contextBag.Set(processMessageEventArgs);

                    var messageContext = new MessageContext(messageId, headers, body, transportTransaction, ReceiveAddress, contextBag);

                    await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);

                    await processMessageEventArgs.SafeCompleteMessageAsync(message,
                            transportSettings.TransportTransactionMode,
                            transaction,
                            cancellationToken: messageProcessingCancellationToken)
                        .ConfigureAwait(false);

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

                        var errorContext = new ErrorContext(ex, message.GetNServiceBusHeaders(), messageId, body,
                            transportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);

                        result = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await processMessageEventArgs.SafeCompleteMessageAsync(message,
                                    transportSettings.TransportTransactionMode,
                                    transaction,
                                    cancellationToken: messageProcessingCancellationToken)
                                .ConfigureAwait(false);
                        }

                        transaction?.Commit();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await processMessageEventArgs.SafeAbandonMessageAsync(message,
                                transportSettings.TransportTransactionMode,
                                cancellationToken: messageProcessingCancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch (ServiceBusException onErrorEx) when (onErrorEx.IsTransient || onErrorEx.Reason is ServiceBusFailureReason.MessageLockLost)
                {
                    Logger.Debug("Failed to execute recoverability.", onErrorEx);

                    await processMessageEventArgs.SafeAbandonMessageAsync(message,
                            transportSettings.TransportTransactionMode,
                            cancellationToken: messageProcessingCancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception onErrorEx) when (onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                {
                    throw;
                }
                catch (Exception onErrorEx)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorEx, messageProcessingCancellationToken);

                    await processMessageEventArgs.SafeAbandonMessageAsync(message,
                            transportSettings.TransportTransactionMode,
                            cancellationToken: messageProcessingCancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        CommittableTransaction CreateTransaction() =>
            transportSettings.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
                ? new CommittableTransaction(new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable,
                    Timeout = TransactionManager.MaximumTimeout
                })
                : null;

        TransportTransaction CreateTransportTransaction(string incomingQueuePartitionKey, CommittableTransaction transaction)
        {
            var transportTransaction = new TransportTransaction();

            if (transportSettings.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive)
            {
                transportTransaction.Set(serviceBusClient);
                transportTransaction.Set("IncomingQueue.PartitionKey", incomingQueuePartitionKey);
                transportTransaction.Set(transaction);
            }

            return transportTransaction;
        }

        public ISubscriptionManager Subscriptions { get; }

        public string Id { get; }

        public string ReceiveAddress { get; }
    }
}