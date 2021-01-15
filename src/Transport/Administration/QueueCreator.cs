namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Microsoft.Azure.ServiceBus.Primitives;

    class QueueCreator
    {
        private readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMB;

        public QueueCreator(
            AzureServiceBusTransport transportSettings,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.connectionStringBuilder = connectionStringBuilder;
            this.namespacePermissions = namespacePermissions;
            this.maxSizeInMB = transportSettings.EntityMaximumSize * 1024;
        }

        public async Task CreateQueues(string[] queues)
        {
            var result = await namespacePermissions.CanManage().ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new Exception(result.ErrorMessage);
            }

            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                var topic = new TopicDescription(transportSettings.TopicName)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMB = maxSizeInMB
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
                        MaxSizeInMB = maxSizeInMB,
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

        public async Task CreateSubscription(string subscribingQueue)
        {
            var result = await namespacePermissions.CanManage().ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new Exception(result.ErrorMessage);
            }

            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                var subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
                var subscription = new SubscriptionDescription(transportSettings.TopicName, subscriptionName)
                {
                    LockDuration = TimeSpan.FromMinutes(5),
                    ForwardTo = subscribingQueue,
                    EnableDeadLetteringOnFilterEvaluationExceptions = false,
                    MaxDeliveryCount = int.MaxValue,
                    EnableBatchedOperations = true,
                    UserMetadata = subscribingQueue
                };

                try
                {
                    await client.CreateSubscriptionAsync(subscription, new RuleDescription("$default", new FalseFilter())).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
                // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                {
                }
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}