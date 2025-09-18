namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

class TopologyCreator(AzureServiceBusTransport transportSettings)
{
    public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues, string? instanceName = null,
        CancellationToken cancellationToken = default)
    {
        var topologyCreator = new MigrationTopologyCreator(transportSettings);
        await topologyCreator.Create(adminClient, cancellationToken).ConfigureAwait(false);

        var queueCreator = new QueueCreator(transportSettings);
        await queueCreator.Create(adminClient, queues, instanceName, cancellationToken).ConfigureAwait(false);
    }
}