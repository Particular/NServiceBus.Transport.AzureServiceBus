namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;

sealed class NullSubscriptionForwarders : ISubscriptionForwarders
{
    public Task StartForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
}