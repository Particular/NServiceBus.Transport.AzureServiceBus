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

    // Not thread-safe: Start, Stop and DisposeAsync must not be called concurrently, which SubscriptionForwarders guarantees.
    // Only running is read concurrently, by the error handler of the processor.
    Resources? running;
    Task released = Task.CompletedTask;
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

    public async Task Start(CancellationToken cancellationToken = default)
    {
        if (disposed || running != null)
        {
            return;
        }

        // A canceled stop might still be releasing the previous processor
        await released.WaitAsync(cancellationToken).ConfigureAwait(false);

        var client = clientFactory();
        var processor = client.CreateSessionProcessor(topicName, subscriptionName, new ServiceBusSessionProcessorOptions
        {
            //HINT: We intentionally use conservative values here to not overwhelm the broker
            //      There is no evidence yet that these values need to be configurable
            PrefetchCount = 10,
            MaxConcurrentSessions = 16,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            Identifier = $"Forwarding-Processor-{topicName}-{subscriptionName}-{inputQueueAddress}",
            AutoCompleteMessages = false,
        });
        var sender = client.CreateSender(inputQueueAddress, new ServiceBusSenderOptions
        {
            Identifier = $"Forwarding-Sender-{topicName}-{subscriptionName}"
        });
        var resources = new Resources(client, processor, sender);

        processor.ProcessErrorAsync += args => OnError(resources, args);
        processor.ProcessMessageAsync += args => OnMessage(sender, args);

        // Assigned before starting, so that errors reported while starting are not mistaken for errors of a stopped processor
        Volatile.Write(ref running, resources);
        var started = false;
        try
        {
            await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
            started = true;
        }
        finally
        {
            if (!started)
            {
                // Roll back so that nothing is left running and a later start creates everything again
                Volatile.Write(ref running, null);
                await Release(resources).ConfigureAwait(false);
            }
        }
    }

    // The caller's token only bounds how long Stop waits; once started, releasing always completes so nothing is left running
    public Task Stop(CancellationToken cancellationToken = default)
    {
        if (running is { } resources)
        {
            // Cleared first, so that late errors of the stopping processor are ignored
            Volatile.Write(ref running, null);
            released = Release(resources);
        }

        return released.WaitAsync(cancellationToken);
    }

#pragma warning disable PS0018 // Releasing cannot be canceled, see Stop
    async Task Release(Resources resources)
#pragma warning restore PS0018
    {
        try
        {
            // A canceled stop would leave the processor running
            await resources.Processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to stop the forwarder for subscription '{subscriptionName}' on topic '{topicName}'.", ex);
        }

        await DisposeLoggingFailure(resources.Processor, "session processor").ConfigureAwait(false);
        await DisposeLoggingFailure(resources.Sender, "sender").ConfigureAwait(false);
        await DisposeLoggingFailure(resources.Client, "client").ConfigureAwait(false);
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

#pragma warning disable PS0018
    async Task OnMessage(ServiceBusSender sender, ProcessSessionMessageEventArgs arg)
#pragma warning restore PS0018
    {
        using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var serviceBusMessage = new ServiceBusMessage(arg.Message);

        await arg.CompleteMessageAsync(arg.Message, arg.CancellationToken).ConfigureAwait(false);
        await sender.SendMessageAsync(serviceBusMessage, arg.CancellationToken).ConfigureAwait(false);
        arg.ReleaseSession();
        ts.Complete();
        circuitBreaker.Success();
    }

#pragma warning disable PS0018
    async Task OnError(Resources resources, ProcessErrorEventArgs arg)
#pragma warning restore PS0018
    {
        string message = $"Failed to receive a message on pump '{arg.Identifier}' listening on '{arg.EntityPath}' connected to '{arg.FullyQualifiedNamespace}' due to '{arg.ErrorSource}'. Exception: {arg.Exception}";

        // Errors of a processor that is being stopped must not affect the forwarder
        if (Volatile.Read(ref running) != resources)
        {
            Logger.Debug(message, arg.Exception);
            return;
        }

        // Making sure transient exceptions do not trigger the circuit breaker.
        if (arg.Exception is ServiceBusException { IsTransient: true })
        {
            Logger.Debug(message, arg.Exception);
            return;
        }

        // Everything else, including a subscription that was deleted after the forwarder started, is handled by the circuit breaker
        Logger.Warn(message, arg.Exception);
        await circuitBreaker.Failure(arg.Exception, arg.CancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;

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

    sealed class Resources(ServiceBusClient client, ServiceBusSessionProcessor processor, ServiceBusSender sender)
    {
        public ServiceBusClient Client { get; } = client;
        public ServiceBusSessionProcessor Processor { get; } = processor;
        public ServiceBusSender Sender { get; } = sender;
    }
}
