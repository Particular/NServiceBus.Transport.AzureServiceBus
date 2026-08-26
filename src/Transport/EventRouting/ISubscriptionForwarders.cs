namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;

interface ISubscriptionForwarders
{
    Task StartForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default);
    Task StopForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default);
}