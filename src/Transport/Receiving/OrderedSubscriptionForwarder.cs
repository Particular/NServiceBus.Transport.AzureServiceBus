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
    ServiceBusClient? forwardingClient;
    ServiceBusSessionProcessor? sessionProcessor;
    ServiceBusSender? sender;
    readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
    readonly Func<ServiceBusClient> clientFactory;
    readonly string topicName;
    readonly string subscriptionName;
    readonly string inputQueueAddress;

    // Stop and DisposeAsync can overlap with Start, and the error handler can release the resources at any time
    readonly object stateLock = new();
    bool running;
    bool stopRequested;
    // Set when the forwarder is registered again while running, e.g. by a Subscribe that provisioned the subscription.
    // A not-found after that can stem from accepts issued before the subscription existed.
    bool subscriptionConfirmed;
    TaskCompletionSource? startCompletion;
    Task releasingResources = Task.CompletedTask;

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
        TaskCompletionSource completion;
        Task pendingRelease;
        lock (stateLock)
        {
            if (running)
            {
                subscriptionConfirmed = true;
                return Task.CompletedTask;
            }

            if (startCompletion != null)
            {
                return Task.CompletedTask;
            }

            completion = BeginStarting();
            pendingRelease = releasingResources;
        }

        return StartAfterRelease(completion, pendingRelease, cancellationToken);
    }

    // Must be called while holding stateLock
    TaskCompletionSource BeginStarting()
    {
        stopRequested = false;
        startCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return startCompletion;
    }

    async Task StartAfterRelease(TaskCompletionSource completion, Task pendingRelease, CancellationToken cancellationToken)
    {
        try
        {
            await pendingRelease.ConfigureAwait(false);

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
            var processor = client.CreateSessionProcessor(topicName, subscriptionName, sessionReceiveOptions);
            processor.ProcessErrorAsync += OnError;
            processor.ProcessMessageAsync += OnMessage;

            var processorSender = client.CreateSender(inputQueueAddress, new ServiceBusSenderOptions
            {
                Identifier = $"Forwarding-Sender-{topicName}-{subscriptionName}"
            });

            lock (stateLock)
            {
                forwardingClient = client;
                sessionProcessor = processor;
                sender = processorSender;
                running = true;
                subscriptionConfirmed = false;

                if (stopRequested)
                {
                    _ = BeginReleasingResources(CancellationToken.None);
                    return;
                }
            }

            await processor.StartProcessingAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            lock (stateLock)
            {
                startCompletion = null;
            }

            completion.SetResult();
        }
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        Task? pendingStart = null;
        lock (stateLock)
        {
            if (!running && startCompletion != null)
            {
                stopRequested = true;
                pendingStart = startCompletion.Task;
            }
        }

        if (pendingStart != null)
        {
            await pendingStart.ConfigureAwait(false);
        }

        Task releasing;
        lock (stateLock)
        {
            _ = BeginReleasingResources(cancellationToken);
            releasing = releasingResources;
        }

        await releasing.ConfigureAwait(false);
    }

    // Must be called while holding stateLock. Returns false when the resources are already released or being released.
    bool BeginReleasingResources(CancellationToken cancellationToken)
    {
        if (!running)
        {
            return false;
        }

        running = false;
        // Stopping the processor waits for its running handlers, which might include the caller
        releasingResources = Task.Run(() => ReleaseResources(cancellationToken), CancellationToken.None);
        return true;
    }

    async Task ReleaseResources(CancellationToken cancellationToken)
    {
        ServiceBusSessionProcessor processor;
        ServiceBusSender processorSender;
        ServiceBusClient client;
        lock (stateLock)
        {
            processor = sessionProcessor!;
            processorSender = sender!;
            client = forwardingClient!;
        }

        try
        {
            await processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
            await processor.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Operation canceled while stopping the forwarder {processor.EntityPath}.", ex);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to stop the forwarder {processor.EntityPath}.", ex);
        }
        finally
        {
            processor.ProcessErrorAsync -= OnError;
            processor.ProcessMessageAsync -= OnMessage;

            lock (stateLock)
            {
                sessionProcessor = null;
                sender = null;
                forwardingClient = null;
            }

            await DisposeLoggingFailure(processor, "session processor").ConfigureAwait(false);
            await DisposeLoggingFailure(processorSender, "sender").ConfigureAwait(false);
            await DisposeLoggingFailure(client, "client").ConfigureAwait(false);
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

#pragma warning disable PS0018 // Only Stop ends a restart, through stopRequested
    async Task Restart(TaskCompletionSource completion, Task pendingRelease)
#pragma warning restore PS0018
    {
        try
        {
            await StartAfterRelease(completion, pendingRelease, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to restart the forwarder for subscription '{subscriptionName}' on topic '{topicName}'. Forwarding resumes after the endpoint is restarted or subscribes to the event again.", ex);
        }
    }

#pragma warning disable PS0018
    async Task OnMessage(ProcessSessionMessageEventArgs arg)
#pragma warning restore PS0018
    {
        using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var serviceBusMessage = new ServiceBusMessage(arg.Message);

        await arg.CompleteMessageAsync(arg.Message, arg.CancellationToken).ConfigureAwait(false);
        await sender!.SendMessageAsync(serviceBusMessage, arg.CancellationToken).ConfigureAwait(false);
        arg.ReleaseSession();
        ts.Complete();
        circuitBreaker.Success();
    }

#pragma warning disable PS0018
    async Task OnError(ProcessErrorEventArgs arg)
#pragma warning restore PS0018
    {
        // Releasing the processor, sender and client keeps a missing subscription from holding a connection and polling it forever.
        // Only receive-side errors mean the subscription is missing; a not-found from forwarding the message concerns the input queue.
        if (arg.Exception is ServiceBusException { Reason: ServiceBusFailureReason.MessagingEntityNotFound }
            && arg.ErrorSource is ServiceBusErrorSource.AcceptSession or ServiceBusErrorSource.Receive)
        {
            bool goingDormant;
            TaskCompletionSource? restartCompletion = null;
            Task pendingRelease = Task.CompletedTask;
            lock (stateLock)
            {
                goingDormant = BeginReleasingResources(CancellationToken.None);

                if (goingDormant && subscriptionConfirmed)
                {
                    goingDormant = false;
                    restartCompletion = BeginStarting();
                    pendingRelease = releasingResources;
                }
            }

            if (restartCompletion != null)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Restarting the forwarder for subscription '{0}' on topic '{1}' because it was not found before it was confirmed to exist.", subscriptionName, topicName);
                }

                _ = Task.Run(() => Restart(restartCompletion, pendingRelease), CancellationToken.None);
            }
            else if (goingDormant)
            {
                Logger.WarnFormat("Subscription '{0}' on topic '{1}' does not exist, so its events cannot be forwarded to '{2}'. Provision the subscription or remove the topology mapping. Forwarding resumes after the subscription is provisioned and the endpoint is restarted or subscribes to the event again.", subscriptionName, topicName, inputQueueAddress);
            }

            return;
        }

        string message = $"Failed to receive a message on pump '{arg.Identifier}' listening on '{arg.EntityPath}' connected to '{arg.FullyQualifiedNamespace}' due to '{arg.ErrorSource}'. Exception: {arg.Exception}";
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
}