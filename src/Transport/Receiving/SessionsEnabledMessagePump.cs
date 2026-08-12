namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AdvancedExtensibility;
using Azure.Messaging.ServiceBus;
using BitFaster.Caching.Lru;
using Extensibility;
using Logging;

sealed class SessionsEnabledMessagePump(
    ServiceBusClient serviceBusClient,
    AzureServiceBusTransport transportSettings,
    string receiveAddress,
    ReceiveSettings receiveSettings,
    Action<string, Exception, CancellationToken> criticalErrorAction,
    ISubscriptionManager? subscriptionManager)
    : IMessageReceiver, IAsyncDisposable
{
    readonly FastConcurrentLru<string, bool> messagesToBeCompleted = new(1_000);

    OnMessage? onMessage;
    OnError? onError;
    RepeatedFailuresOverTimeCircuitBreaker? circuitBreaker;

    // Start
    CancellationTokenSource? messageProcessingCancellationTokenSource;
    ServiceBusSessionProcessor? sessionProcessor;

    static readonly ILog Logger = LogManager.GetLogger<SessionsEnabledMessagePump>();

    int autoForwardWarningLogged;

    PushRuntimeSettings? limitations;

    [MemberNotNull(nameof(limitations), nameof(onMessage), nameof(onError))]
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
        var sessionReceiveOptions = new ServiceBusSessionProcessorOptions
        {
            PrefetchCount = CalculatePrefetchCount(limitations!.MaxConcurrency),
            ReceiveMode = TransactionMode == TransportTransactionMode.None
                ? ServiceBusReceiveMode.ReceiveAndDelete
                : ServiceBusReceiveMode.PeekLock,
            Identifier = $"Processor-{Id}-{ReceiveAddress}-{Guid.NewGuid()}",
            MaxConcurrentSessions = limitations.MaxConcurrency,
            AutoCompleteMessages = false
        };
        if (transportSettings.MaxAutoLockRenewalDuration.HasValue)
        {
            sessionReceiveOptions.MaxAutoLockRenewalDuration = transportSettings.MaxAutoLockRenewalDuration.Value;
        }

        sessionProcessor = serviceBusClient.CreateSessionProcessor(ReceiveAddress, sessionReceiveOptions);
        sessionProcessor.ProcessErrorAsync += OnProcessorError;
        sessionProcessor.ProcessMessageAsync += OnProcessMessage;

        messageProcessingCancellationTokenSource = new CancellationTokenSource();

        circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"'{receiveSettings.ReceiveAddress}'",
            transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker, ex =>
            {
                criticalErrorAction("Failed to receive message from Azure Service Bus.", ex,
                    messageProcessingCancellationTokenSource.Token);
            }, () => UpdateProcessingCapacity(1),
            () => UpdateProcessingCapacity(limitations.MaxConcurrency));

        await sessionProcessor.StartProcessingAsync(cancellationToken)
        .ConfigureAwait(false);
    }

    TransportTransactionMode TransactionMode => transportSettings.TransportTransactionMode;

    int CalculatePrefetchCount(int maxConcurrency)
    {
        var prefetchCount = maxConcurrency * transportSettings.PrefetchMultiplier;

        if (transportSettings.PrefetchCount.HasValue)
        {
            prefetchCount = transportSettings.PrefetchCount.Value;
        }

        return prefetchCount;
    }

#pragma warning disable PS0018
    async Task OnProcessMessage(ProcessSessionMessageEventArgs arg)
#pragma warning restore PS0018
    {
        string nativeMessageId;
        Dictionary<string, string?> headers;
        BinaryData body;
        var message = arg.Message;

        circuitBreaker!.Success();

        try
        {
            nativeMessageId = message.GetMessageId();

            // Deliberately not using the cancellation token to make sure we abandon the message even when the
            // cancellation token is already set.
            if (await arg.TrySafeCompleteMessage(message, TransactionMode, messagesToBeCompleted, CancellationToken.None).ConfigureAwait(false))
            {
                return;
            }

            // Deliberately not using the cancellation token to make sure we abandon the message even when the
            // cancellation token is already set.
            if (await arg.TrySafeAbandonMessage(message, TransactionMode, CancellationToken.None).ConfigureAwait(false))
            {
                return;
            }

            headers = message.GetNServiceBusHeaders();
            body = message.GetBody();
        }
        catch (Exception ex)
        {
            await arg.SafeDeadLetterMessage(message, TransactionMode, ex, CancellationToken.None).ConfigureAwait(false);

            return;
        }

        // need to catch OCE here because we are switching token
        try
        {
            await ProcessMessage(message, arg, nativeMessageId, headers, body, messageProcessingCancellationTokenSource!.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationTokenSource!.Token))
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Message processing canceled.", ex);
            }
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
        await circuitBreaker!.Failure(processErrorEventArgs.Exception, processErrorEventArgs.CancellationToken)
            .ConfigureAwait(false);
    }

    public Task ChangeConcurrency(PushRuntimeSettings newLimitations, CancellationToken cancellationToken = default)
    {
        limitations = newLimitations;

        UpdateProcessingCapacity(limitations.MaxConcurrency);

        return Task.CompletedTask;
    }

    void UpdateProcessingCapacity(int maxConcurrency)
    {
        sessionProcessor!.UpdateConcurrency(maxConcurrency, 1);
        sessionProcessor!.UpdatePrefetchCount(CalculatePrefetchCount(maxConcurrency));
    }

    public async Task StopReceive(CancellationToken cancellationToken = default)
    {
        if (messageProcessingCancellationTokenSource is null)
        {
            // Receiver hasn't been started or is already stopped
            return;
        }

        // Wiring up the stop token to trigger the cancellation token that is being
        // used inside the message handling pipeline
        await using var _ = cancellationToken
            .Register(state => (state as CancellationTokenSource)?.Cancel(),
                messageProcessingCancellationTokenSource,
                useSynchronizationContext: false).ConfigureAwait(false);
        // Deliberately not passing the cancellation token forward in order to make sure
        // the processor waits until all processing handlers have returned. This makes
        // the code compliant to the previous version that uses manual receives and is aligned
        // with how the cancellation token support was initially designed.
        await sessionProcessor!.StopProcessingAsync(CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            await sessionProcessor.CloseAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Operation canceled while stopping the receiver {sessionProcessor.EntityPath}.", ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (sessionProcessor != null)
        {
            sessionProcessor.ProcessErrorAsync -= OnProcessorError;
            sessionProcessor.ProcessMessageAsync -= OnProcessMessage;

            await sessionProcessor.DisposeAsync().ConfigureAwait(false);
            sessionProcessor = null;
        }

        messageProcessingCancellationTokenSource?.Dispose();
        messageProcessingCancellationTokenSource = null;

        circuitBreaker?.Dispose();
        circuitBreaker = null;
    }

    async Task ProcessMessage(ServiceBusReceivedMessage message,
        ProcessSessionMessageEventArgs processMessageEventArgs,
        string nativeMessageId, Dictionary<string, string?> headers, BinaryData body,
        CancellationToken messageProcessingCancellationToken)
    {
        // args.CancellationToken is currently not used because the v8 version that supports cancellation was designed
        // to not flip the cancellation token until the very last moment in time when the stop token is flipped.
        var contextBag = new ContextBag();
        contextBag.Set(message);
        contextBag.Set(processMessageEventArgs);

        // Pass on the SessionId so it can be propagated by Core for delayed retries, error or audit
        var receiveProperties = new ReceiveProperties(new Dictionary<string, string> { ["SessionId"] = message.SessionId });

        try
        {
            using var azureServiceBusTransaction = CreateTransaction(message.PartitionKey);
            var messageContext = new MessageContext(nativeMessageId, headers, body, receiveProperties, azureServiceBusTransaction.TransportTransaction, ReceiveAddress, contextBag);

            await onMessage!(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);

            await processMessageEventArgs.SafeCompleteMessage(message,
                    TransactionMode,
                    azureServiceBusTransaction,
                    messagesToBeCompleted,
                    cancellationToken: messageProcessingCancellationToken)
                .ConfigureAwait(false);

            azureServiceBusTransaction.Commit();
        }
        catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
        {
            try
            {
                using var azureServiceBusTransaction = CreateTransaction(message.PartitionKey);

                var errorContext = new ErrorContext(ex, message.GetNServiceBusHeaders(), nativeMessageId, body,
                    receiveProperties, azureServiceBusTransaction.TransportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);

                var result = await onError!(errorContext, messageProcessingCancellationToken).ConfigureAwait(false);

                switch (result)
                {
                    case ErrorHandleResult.RetryRequired:
                        await processMessageEventArgs.SafeAbandonMessage(message,
                                TransactionMode,
                                cancellationToken: messageProcessingCancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case ErrorHandleResult.Handled:
                        if (azureServiceBusTransaction.TransportTransaction.TryGet<DeadLetterRequest>(out var deadLetterRequest))
                        {
                            WarnIfDeadLetteringWithoutForwarding(nativeMessageId, message.DeliveryCount);
                            await processMessageEventArgs.SafeDeadLetterMessage(message,
                                    TransactionMode,
                                    deadLetterRequest,
                                    cancellationToken: messageProcessingCancellationToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await processMessageEventArgs.SafeCompleteMessage(message,
                                    TransactionMode,
                                    azureServiceBusTransaction,
                                    messagesToBeCompleted,
                                    cancellationToken: messageProcessingCancellationToken)
                                .ConfigureAwait(false);

                            azureServiceBusTransaction.Commit();
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown error handle result");
                }
            }
            catch (ServiceBusException onErrorEx) when (onErrorEx.IsTransient || onErrorEx.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                Logger.Debug("Failed to execute recoverability.", onErrorEx);

                await processMessageEventArgs.SafeAbandonMessage(message,
                        TransactionMode,
                        cancellationToken: messageProcessingCancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(messageProcessingCancellationToken))
            {
                criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.MessageId}`", onErrorEx, messageProcessingCancellationToken);

                await processMessageEventArgs.SafeAbandonMessage(message,
                        TransactionMode,
                        cancellationToken: messageProcessingCancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    AzureServiceBusTransportTransaction CreateTransaction(string incomingQueuePartitionKey) =>
        TransactionMode == TransportTransactionMode.SendsAtomicWithReceive
            ? new AzureServiceBusTransportTransaction(serviceBusClient, incomingQueuePartitionKey,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable,
                    Timeout = TransactionManager.DefaultTimeout
                })
            : new AzureServiceBusTransportTransaction();

    void WarnIfDeadLetteringWithoutForwarding(string nativeMessageId, int deliveryCount)
    {
        if (transportSettings.AutoForwardDeadLetteredMessagesToErrorQueue != null || Interlocked.CompareExchange(ref autoForwardWarningLogged, 1, 0) != 0)
        {
            return;
        }

        Logger.Warn($"Message '{nativeMessageId}' (delivery count: {deliveryCount}) is being moved to the dead-letter queue. " +
                    "Dead-lettered messages will not automatically appear in the error queue. " +
                    "Consider setting 'AutoForwardDeadLetteredMessagesToErrorQueue = true' on the transport to automatically forward dead-lettered messages to the error queue, " +
                    "making them visible for monitoring and reprocessing. Other messages moved to the dead-letter queue will not be logged to avoid flooding the logs." +
                    "To suppress this warning, explicitly set 'AutoForwardDeadLetteredMessagesToErrorQueue = false'.");
    }

    public ISubscriptionManager? Subscriptions { get; } = subscriptionManager;

    public string Id { get; } = receiveSettings.Id;

    public string ReceiveAddress { get; } = receiveAddress;
}