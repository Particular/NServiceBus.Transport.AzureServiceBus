namespace NServiceBus.Transport.AzureServiceBus.Receiving;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Logging;

class OrderedSubscriptionForwarder : IAsyncDisposable
{
    static readonly ILog Logger = LogManager.GetLogger<OrderedSubscriptionForwarder>();

    HashSet<string> eventTypes = [];
    readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
    readonly Func<ServiceBusClient> clientFactory;
    readonly string topicName;
    readonly string subscriptionName;
    readonly string inputQueueAddress;

    // All lifecycle decisions are made under stateLock; each run owns its client, processor and sender
    readonly object stateLock = new();
    ForwardingRun? activeRun;
    ForwardingRun? lastRun;
    bool disposed;

    public OrderedSubscriptionForwarder(Func<ServiceBusClient> clientFactory, string topicName, string subscriptionName, string inputQueueAddress, TimeSpan timeBeforeTriggeringCircuitBreaker, Action<string, Exception, CancellationToken> criticalErrorAction)
    {
        this.clientFactory = clientFactory;
        this.topicName = topicName;
        this.subscriptionName = subscriptionName;
        this.inputQueueAddress = inputQueueAddress;

        circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker($"Forwarding-Processor-{topicName}-{subscriptionName}-{inputQueueAddress}",
            timeBeforeTriggeringCircuitBreaker, ex =>
            {
                criticalErrorAction("Failed to receive message from Azure Service Bus.", ex,
                    CancellationToken.None);
            });
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        ForwardingRun run;
        lock (stateLock)
        {
            if (disposed)
            {
                return Task.CompletedTask;
            }

            if (activeRun != null)
            {
                // Registering again means the subscription may exist by now; the starting processor may already have issued accepts.
                // Stop clears activeRun when requesting the stop, so the confirmation always belongs to a run that may still restart.
                activeRun.SubscriptionConfirmed = true;
                return Task.CompletedTask;
            }

            run = BeginRun();
        }

        return StartRun(run, cancellationToken);
    }

    // Must be called while holding stateLock
    ForwardingRun BeginRun()
    {
        var run = new ForwardingRun(lastRun?.Released.Task ?? Task.CompletedTask);
        activeRun = run;
        lastRun = run;
        return run;
    }

    async Task StartRun(ForwardingRun run, CancellationToken cancellationToken)
    {
        var startedOrStopped = false;
        try
        {
            await run.PreviousReleased.ConfigureAwait(false);

            lock (stateLock)
            {
                if (run.StopRequested)
                {
                    BeginRelease(run);
                    startedOrStopped = true;
                    return;
                }
            }

            var sessionReceiveOptions = new ServiceBusSessionProcessorOptions
            {
                //HINT: We intentionally use conservative values here to not overwhelm the broker
                //      There is no evidence yet that these values need to be configurable
                PrefetchCount = 10,
                MaxConcurrentSessions = 16,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                Identifier = $"Forwarding-Processor-{topicName}-{subscriptionName}-{inputQueueAddress}",
                AutoCompleteMessages = false,
            };

            var client = clientFactory();
            lock (stateLock)
            {
                run.Client = client;
            }

            var processor = client.CreateSessionProcessor(topicName, subscriptionName, sessionReceiveOptions);
            processor.ProcessErrorAsync += args => OnError(run, args);
            processor.ProcessMessageAsync += args => OnMessage(run, args);
            var processorSender = client.CreateSender(inputQueueAddress, new ServiceBusSenderOptions
            {
                Identifier = $"Forwarding-Sender-{topicName}-{subscriptionName}"
            });

            lock (stateLock)
            {
                run.Processor = processor;
                run.Sender = processorSender;

                if (run.StopRequested)
                {
                    BeginRelease(run);
                    startedOrStopped = true;
                    return;
                }
            }

            await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);

            lock (stateLock)
            {
                run.Processing = true;
                startedOrStopped = true;

                if (run.StopRequested)
                {
                    BeginRelease(run);
                }
            }
        }
        finally
        {
            if (!startedOrStopped)
            {
                // Roll back a failed start so that a later registration can start the forwarder again
                lock (stateLock)
                {
                    if (activeRun == run)
                    {
                        activeRun = null;
                    }

                    BeginRelease(run);
                }

                await run.Released.Task.ConfigureAwait(false);
            }
        }
    }

    // The caller's token only bounds how long Stop waits; releasing always completes so nothing is left running
    public async Task Stop(CancellationToken cancellationToken = default)
    {
        Task released;
        lock (stateLock)
        {
            if (activeRun is { } run)
            {
                activeRun = null;
                run.StopRequested = true;

                // A run that is still starting is released by its start once it sees the stop request
                if (run.Processing)
                {
                    BeginRelease(run);
                }
            }

            released = lastRun?.Released.Task ?? Task.CompletedTask;
        }

        await released.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    // Must be called while holding stateLock
    void BeginRelease(ForwardingRun run)
    {
        if (!run.ReleaseStarted)
        {
            run.ReleaseStarted = true;
            // Stopping the processor waits for its running handlers, which might include the caller
            _ = Task.Run(() => Release(run), CancellationToken.None);
        }
    }

#pragma warning disable PS0018 // Releasing cannot be canceled, see Stop
    async Task Release(ForwardingRun run)
#pragma warning restore PS0018
    {
        try
        {
            if (run.Processor != null)
            {
                try
                {
                    // A canceled stop would leave the processor running
                    await run.Processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to stop the forwarder for subscription '{subscriptionName}' on topic '{topicName}'.", ex);
                }

                await DisposeLoggingFailure(run.Processor, "session processor").ConfigureAwait(false);
            }

            if (run.Sender != null)
            {
                await DisposeLoggingFailure(run.Sender, "sender").ConfigureAwait(false);
            }

            if (run.Client != null)
            {
                await DisposeLoggingFailure(run.Client, "client").ConfigureAwait(false);
            }
        }
        finally
        {
            run.Released.TrySetResult();
        }
    }

#pragma warning disable PS0018 // Disposing cannot be canceled
    async Task DisposeLoggingFailure(IAsyncDisposable disposable, string resourceName)
#pragma warning restore PS0018
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to dispose the {resourceName} of the forwarder for subscription '{subscriptionName}' on topic '{topicName}'.", ex);
        }
    }

#pragma warning disable PS0018 // Only Stop ends a restart, through its stop request
    async Task Restart(ForwardingRun run)
#pragma warning restore PS0018
    {
        try
        {
            await StartRun(run, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to restart the forwarder for subscription '{subscriptionName}' on topic '{topicName}'. Forwarding resumes after the endpoint is restarted or subscribes to the event again.", ex);
        }
    }

#pragma warning disable PS0018
    async Task OnMessage(ForwardingRun run, ProcessSessionMessageEventArgs arg)
#pragma warning restore PS0018
    {
        using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var serviceBusMessage = new ServiceBusMessage(arg.Message);

        await arg.CompleteMessageAsync(arg.Message, arg.CancellationToken).ConfigureAwait(false);
        await run.Sender!.SendMessageAsync(serviceBusMessage, arg.CancellationToken).ConfigureAwait(false);
        arg.ReleaseSession();
        ts.Complete();
        circuitBreaker.Success();
    }

#pragma warning disable PS0018
    async Task OnError(ForwardingRun run, ProcessErrorEventArgs arg)
#pragma warning restore PS0018
    {
        string message = $"Failed to receive a message on pump '{arg.Identifier}' listening on '{arg.EntityPath}' connected to '{arg.FullyQualifiedNamespace}' due to '{arg.ErrorSource}'. Exception: {arg.Exception}";

        lock (stateLock)
        {
            if (activeRun != run)
            {
                // Late errors from a stopped or replaced run must not affect the current one
                Logger.Debug(message, arg.Exception);
                return;
            }
        }

        // Releasing the processor, sender and client keeps a missing subscription from holding a connection and polling it forever.
        // Only receive-side errors mean the subscription is missing; a not-found from forwarding the message concerns the input queue.
        if (arg.Exception is ServiceBusException { Reason: ServiceBusFailureReason.MessagingEntityNotFound }
            && arg.ErrorSource is ServiceBusErrorSource.AcceptSession or ServiceBusErrorSource.Receive)
        {
            ForwardingRun? restart = null;
            lock (stateLock)
            {
                // Stop clears the active run under the same lock, so no restart is scheduled after a stop request
                if (activeRun != run)
                {
                    return;
                }

                activeRun = null;
                BeginRelease(run);

                if (run.SubscriptionConfirmed)
                {
                    restart = BeginRun();
                }
            }

            if (restart != null)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Restarting the forwarder for subscription '{0}' on topic '{1}' because it was not found before it was confirmed to exist.", subscriptionName, topicName);
                }

                _ = Task.Run(() => Restart(restart), CancellationToken.None);
            }
            else
            {
                Logger.WarnFormat("Subscription '{0}' on topic '{1}' does not exist, so its events cannot be forwarded to '{2}'. Provision the subscription or remove the topology mapping. Forwarding resumes after the subscription is provisioned and the endpoint is restarted or subscribes to the event again.", subscriptionName, topicName, inputQueueAddress);
            }

            return;
        }

        // Making sure transient exceptions do not trigger the circuit breaker.
        if (arg.Exception is ServiceBusException { IsTransient: true })
        {
            Logger.Debug(message, arg.Exception);
            return;
        }

        Logger.Warn(message, arg.Exception);
        await circuitBreaker.Failure(arg.Exception, arg.CancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        lock (stateLock)
        {
            disposed = true;
        }

        await Stop(CancellationToken.None).ConfigureAwait(false);

        circuitBreaker.Dispose();
    }

    public void RegisterType(string eventTypeFullName)
    {
        eventTypes.Add(eventTypeFullName);
    }

    public bool UnregisterType(string eventTypeFullName)
    {
        eventTypes.Remove(eventTypeFullName);
        return eventTypes.Count == 0;
    }

    // Guarded by stateLock of the forwarder
    sealed class ForwardingRun(Task previousReleased)
    {
        public Task PreviousReleased { get; } = previousReleased;
        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ServiceBusClient? Client { get; set; }
        public ServiceBusSessionProcessor? Processor { get; set; }
        public ServiceBusSender? Sender { get; set; }
        public bool Processing { get; set; }
        public bool SubscriptionConfirmed { get; set; }
        public bool StopRequested { get; set; }
        public bool ReleaseStarted { get; set; }
    }
}