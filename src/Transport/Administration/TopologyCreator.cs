namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

class TopologyCreator(AzureServiceBusTransport transportSettings)
{
    public async Task Create(ServiceBusAdministrationClient adminClient, (string Queue, bool IsSessionEnabled)[] queues, string? instanceName = null,
        CancellationToken cancellationToken = default)
    {
        var topologyCreator = new MigrationTopologyCreator(transportSettings);
        await topologyCreator.Create(adminClient, cancellationToken).ConfigureAwait(false);

        var queueCreator = new QueueCreator();
        await queueCreator.Create(adminClient, queues, cancellationToken).ConfigureAwait(false);
    }
}