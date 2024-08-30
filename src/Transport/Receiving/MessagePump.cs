namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using BitFaster.Caching.Lru;
    using Extensibility;
    using Logging;

    class MessagePump : IMessageReceiver
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ReceiveSettings receiveSettings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly ServiceBusClient serviceBusClient;
        readonly FastConcurrentLru<string, bool> messagesToBeDeleted = new(1_000);

        OnMessage onMessage;
        OnError onError;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        // Start
        CancellationTokenSource messageProcessingCancellationTokenSource;
        ServiceBusProcessor processor;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();

        PushRuntimeSettings limitations;

        public MessagePump(
            ServiceBusClient serviceBusClient,
            AzureServiceBusTransport transportSettings,
            string receiveAddress,
            ReceiveSettings receiveSettings,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            SubscriptionManager subscriptionManager)
        {
            Id = receiveSettings.Id;
            ReceiveAddress = receiveAddress;
            this.serviceBusClient = serviceBusClient;
            this.transportSettings = transportSettings;
            this.receiveSettings = receiveSettings;
            this.criticalErrorAction = criticalErrorAction;
            Subscriptions = subscriptionManager;
        }

        public Task Initialize(
            PushRuntimeSettings limitations,
            OnMessage onMessage,
            OnError onError,
            CancellationToken cancellationToken = default)
        {
            if (receiveSettings.PurgeOnStartup)
            {
                throw new Exception("Azure Service Bus transport doesn't support PurgeOnStartup behavior");
            }

            this.limitations = limitations;
            this.onMessage = onMessage;
            this.onError = onError;

            return Task.CompletedTask;
        }

        public async Task StartReceive(CancellationToken cancellationToken = default)
        {
            int prefetchCount = CalculatePrefetchCount();

            var receiveOptions = new ServiceBusProcessorOptions
            {
                PrefetchCount = prefetchCount,
                ReceiveMode = transportSettings.TransportTransactionMode == TransportTransactionMode.None
                    ? ServiceBusReceiveMode.ReceiveAndDelete
                    : ServiceBusReceiveMode.PeekLock,
                Identifier = $"Processor-{Id}-{ReceiveAddress}-{Guid.NewGuid()}",
                MaxConcurrentCalls = limitations.MaxConcurrency,
                AutoCompleteMessages = false
            };

            if (transportSettings.MaxAutoLockRenewalDuration.HasValue)
            {
                receiveOptions.MaxAutoLockRenewalDuration = transportSettings.MaxAutoLockRenewalDuration.Value;
            }

            processor = serviceBusClient.CreateProcessor(ReceiveAddress, receiveOptions);
            processor.ProcessErrorAsync += OnProcessorError;
            processor.ProcessMessageAsync += OnProcessMessage;

            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'",
                transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, ex =>
                {
                    criticalErrorAction("Failed to receive message from Azure Service Bus.", ex,
                        messageProcessingCancellationTokenSource.Token);
                }, () =>
                {
                    //We don't have to update the prefetch count since we are failing to receive anyway
                    processor.UpdateConcurrency(1);
                },
                () =>
                {
                    processor.UpdateConcurrency(limitations.MaxConcurrency);
                });

            await processor.StartProcessingAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int CalculatePrefetchCount()
        {
            var prefetchCount = limitations.MaxConcurrency * transportSettings.PrefetchMultiplier;

            if (transportSettings.PrefetchCount.HasValue)
            {
                prefetchCount = transportSettings.PrefetchCount.Value;
            }

            return prefetchCount;
        }

#pragma warning disable PS0018
        async Task DeleteMessage(ProcessMessageEventArgs processMessageEventArgs, ServiceBusReceivedMessage message)
#pragma warning restore PS0018
        {
            try
            {
                using (var azureServiceBusTransaction = CreateTransaction(message.PartitionKey))
                {
                    await processMessageEventArgs.SafeCompleteMessageAsync(message,
                                transportSettings.TransportTransactionMode,
                                azureServiceBusTransaction, CancellationToken.None)
                            .ConfigureAwait(false);

                    azureServiceBusTransaction.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to delete message with id '{message.GetMessageId()}'. This message will be returned to the queue", ex);

                messagesToBeDeleted.AddOrUpdate(message.GetMessageId(), true);
            }
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

                if (messagesToBeDeleted.TryGet(messageId, out _))
                {
                    await DeleteMessage(arg, message).ConfigureAwait(false);
                    return;
                }

                if (processor.ReceiveMode == ServiceBusReceiveMode.PeekLock && message.LockedUntil < DateTimeOffset.UtcNow)
                {
                    Logger.Warn(
                        $"Skip handling the message with id '{messageId}' because the lock has expired at '{message.LockedUntil}'. " +
                        "This is usually an indication that the endpoint prefetches more messages than it is able to handle within the configured" +
                        " peek lock duration. Consider tweaking the prefetch configuration to values that are better aligned with the concurrency" +
                        " of the endpoint and the time it takes to handle the messages.");

                    try
                    {
                        // Deliberately not using the cancellation token to make sure we abandon the message even when the
                        // cancellation token is already set.
                        await arg.SafeAbandonMessageAsync(message,
                                transportSettings.TransportTransactionMode,
                                cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (Exception abandonException)
                    {
                        // nothing we can do about it, message will be retried
                        Logger.Debug($"Error abandoning the message with id '{messageId}' because the lock has expired at '{message.LockedUntil}.", abandonException);
                    }
                    return;
                }

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

        public Task ChangeConcurrency(PushRuntimeSettings newLimitations, CancellationToken cancellationToken = default)
        {
            limitations = newLimitations;

            processor.UpdateConcurrency(limitations.MaxConcurrency);

            int prefetchCount = CalculatePrefetchCount();
            processor.UpdatePrefetchCount(prefetchCount);

            return Task.CompletedTask;
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
                using (var azureServiceBusTransaction = CreateTransaction(message.PartitionKey))
                {
                    contextBag.Set(message);
                    contextBag.Set(processMessageEventArgs);

                    var messageContext = new MessageContext(messageId, headers, body, azureServiceBusTransaction.TransportTransaction, ReceiveAddress, contextBag);

                    await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);

                    await processMessageEventArgs.SafeCompleteMessageAsync(message,
                            transportSettings.TransportTransactionMode,
                            azureServiceBusTransaction,
                            cancellationToken: messageProcessingCancellationToken)
                        .ConfigureAwait(false);

                    azureServiceBusTransaction.Commit();
                }
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                try
                {
                    if (IsReceiveOnlyMessageLockLost(ex, message))
                    {
                        Logger.Warn($"Message with id `{message.GetMessageId()}` has been returned to the queue and marked for deletion.  NServiceBus recoverability is being skipped. {ex.Message}");
                        //Since the message lock was lost, we can't complete or abandon the message without throwing an error
                        //Recoverability should be skipped
                        return;
                    }

                    ErrorHandleResult result;

                    using (var azureServiceBusTransaction = CreateTransaction(message.PartitionKey))
                    {
                        var errorContext = new ErrorContext(ex, message.GetNServiceBusHeaders(), messageId, body,
                            azureServiceBusTransaction.TransportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);

                        result = await onError(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await processMessageEventArgs.SafeCompleteMessageAsync(message,
                                    transportSettings.TransportTransactionMode,
                                    azureServiceBusTransaction,
                                    cancellationToken: messageProcessingCancellationToken)
                                .ConfigureAwait(false);
                        }

                        azureServiceBusTransaction.Commit();
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

                    if (IsReceiveOnlyMessageLockLost(ex, message))
                    {
                        Logger.Warn($"Message with id `{message.GetMessageId()}` has been returned to the queue and marked for deletion.- {onErrorEx.Message}");
                        //Since the message lock was lost, we can't complete or abandon the message without throwing an error
                        return;
                    }

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

        bool IsReceiveOnlyMessageLockLost(Exception ex, ServiceBusReceivedMessage message)
        {
            if (ex is ServiceBusException serviceBusException &&
                serviceBusException.Reason == ServiceBusFailureReason.MessageLockLost &&
                transportSettings.TransportTransactionMode == TransportTransactionMode.ReceiveOnly)
            {
                messagesToBeDeleted.AddOrUpdate(message.GetMessageId(), true);
                return true;
            }
            return false;
        }

        AzureServiceBusTransportTransaction CreateTransaction(string incomingQueuePartitionKey) =>
            transportSettings.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
                ? new AzureServiceBusTransportTransaction(serviceBusClient, incomingQueuePartitionKey,
                    new TransactionOptions
                    {
                        IsolationLevel = IsolationLevel.Serializable,
                        Timeout = TransactionManager.DefaultTimeout
                    })
                : new AzureServiceBusTransportTransaction();

        public ISubscriptionManager Subscriptions { get; }

        public string Id { get; }

        public string ReceiveAddress { get; }
    }
}