namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;

sealed class NullSubscriptionForwarders : ISubscriptionForwarders
{
    public Task Register(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Unregister(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
}