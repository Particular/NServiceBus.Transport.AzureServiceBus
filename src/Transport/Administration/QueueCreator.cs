namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;

    class QueueCreator
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMb;

        public QueueCreator(
            AzureServiceBusTransport transportSettings,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.connectionStringBuilder = connectionStringBuilder;
            this.namespacePermissions = namespacePermissions;
            maxSizeInMb = transportSettings.EntityMaximumSize * 1024;
        }

        public async Task CreateQueues(string[] queues)
        {
            await namespacePermissions.CanManage().ConfigureAwait(false);

            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                var topic = new TopicDescription(transportSettings.TopicName)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMB = maxSizeInMb
                };

                try
                {
                    await client.CreateTopicAsync(topic).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
                // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                {
                }

                foreach (var address in queues)
                {
                    var queue = new QueueDescription(address)
                    {
                        EnableBatchedOperations = true,
                        LockDuration = TimeSpan.FromMinutes(5),
                        MaxDeliveryCount = int.MaxValue,
                        MaxSizeInMB = maxSizeInMb,
                        EnablePartitioning = transportSettings.EnablePartitioning
                    };

                    try
                    {
                        await client.CreateQueueAsync(queue).ConfigureAwait(false);
                    }
                    catch (MessagingEntityAlreadyExistsException)
                    {
                    }
                    // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                    catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                    {
                    }
                }
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}