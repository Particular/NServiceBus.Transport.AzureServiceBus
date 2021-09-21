namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;

    class QueueCreator
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly string connectionString;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMb;

        public QueueCreator(
            AzureServiceBusTransport transportSettings,
            string connectionString,
            NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.connectionString = connectionString;
            this.namespacePermissions = namespacePermissions;
            maxSizeInMb = transportSettings.EntityMaximumSize * 1024;
        }

        public async Task CreateQueues(string[] queues, CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            var client = transportSettings.TokenCredential != null ? new ServiceBusAdministrationClient(connectionString, transportSettings.TokenCredential) : new ServiceBusAdministrationClient(connectionString);
            var topic = new CreateTopicOptions(transportSettings.TopicName)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = transportSettings.EnablePartitioning,
                MaxSizeInMegabytes = maxSizeInMb
            };

            try
            {
                await client.CreateTopicAsync(topic, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
            // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
            catch (ServiceBusException sbe) when (
                sbe.IsTransient) //when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
            {
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
                    await client.CreateQueueAsync(queue, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason ==
                                                      ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                }
                // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                catch (ServiceBusException sbe) when (
                    sbe.IsTransient) //when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                {
                }
            }

        }
    }
}