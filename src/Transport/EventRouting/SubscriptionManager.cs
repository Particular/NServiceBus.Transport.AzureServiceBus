namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Unicast.Messages;

abstract class SubscriptionManager(
    SubscriptionManagerCreationOptions creationOptions)
    : ISubscriptionManager
{
    protected SubscriptionManagerCreationOptions CreationOptions { get; } = creationOptions;

    public abstract Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default);

    public abstract Task Unsubscribe(MessageMetadata eventType, ContextBag context,
        CancellationToken cancellationToken = default);

    public ValueTask SetupInfrastructureIfNecessary(CancellationToken cancellationToken = default) =>
        CreationOptions.SetupInfrastructure ? SetupInfrastructureCore(cancellationToken) : default;

    protected virtual ValueTask SetupInfrastructureCore(CancellationToken cancellationToken = default) => default;

    // Core's auto-subscribe logs subscribe failures and carries on, so the receivers report them when they start
    public virtual void ThrowIfSubscribingFailed() { }

    public virtual Task StartForwarders(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public abstract Task ShutdownForwarders(CancellationToken cancellationToken = default);
}