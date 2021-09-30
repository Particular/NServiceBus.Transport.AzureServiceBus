namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.Logging;

    class QueueCreator
    {
        static readonly ILog Logger = LogManager.GetLogger<QueueCreator>();

        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusAdministrationClient administrativeClient;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMb;

        public QueueCreator(
            AzureServiceBusTransport transportSettings,
            ServiceBusAdministrationClient administrativeClient,
            NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.administrativeClient = administrativeClient;
            this.namespacePermissions = namespacePermissions;
            maxSizeInMb = transportSettings.EntityMaximumSize * 1024;
        }

        public async Task CreateQueues(string[] queues, CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            var topic = new CreateTopicOptions(transportSettings.TopicName)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = transportSettings.EnablePartitioning,
                MaxSizeInMegabytes = maxSizeInMb
            };

            try
            {
                await administrativeClient.CreateTopicAsync(topic, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Logger.Info($"Topic {topic.Name} already exists");
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Topic creation for {topic.Name} is already in progress");
            }

            foreach (var address in queues)
            {
                var queue = new CreateQueueOptions(address)
                {
                    EnableBatchedOperations = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = int.MaxValue,
                    MaxSizeInMegabytes = maxSizeInMb,
                    EnablePartitioning = transportSettings.EnablePartitioning
                };

                try
                {
                    await administrativeClient.CreateQueueAsync(queue, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Logger.Debug($"Queue {queue.Name} already exists");
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Queue creation for {queue.Name} is already in progress");
                }
            }
        }
    }
}