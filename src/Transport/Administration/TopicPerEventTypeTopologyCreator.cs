namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

class TopicPerEventTypeTopologyCreator(AzureServiceBusTransport transportSettings)
{
    public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues,
        CancellationToken cancellationToken = default)
    {
        var queueCreator = new QueueCreator(transportSettings);
        await queueCreator.Create(adminClient, queues, cancellationToken).ConfigureAwait(false);
    }
}