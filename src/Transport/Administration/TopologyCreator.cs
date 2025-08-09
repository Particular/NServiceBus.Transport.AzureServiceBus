namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Particular.Obsoletes;

[ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
[Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
class TopologyCreator(AzureServiceBusTransport transportSettings)
{
    public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues,
        CancellationToken cancellationToken = default)
    {
        var topologyCreator = new MigrationTopologyCreator(transportSettings);
        await topologyCreator.Create(adminClient, cancellationToken).ConfigureAwait(false);

        var queueCreator = new QueueCreator(transportSettings);
        await queueCreator.Create(adminClient, queues, cancellationToken).ConfigureAwait(false);
    }
}