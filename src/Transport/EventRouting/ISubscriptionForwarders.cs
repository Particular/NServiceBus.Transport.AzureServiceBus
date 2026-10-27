namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;

interface ISubscriptionForwarders
{
    Task Register(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default);
    Task Unregister(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default);
    Task Start(CancellationToken cancellationToken = default);
    Task Shutdown(CancellationToken cancellationToken = default);
}