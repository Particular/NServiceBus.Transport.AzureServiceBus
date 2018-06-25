namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;

    class QueueCreator : ICreateQueues
    {
        readonly string mainInputQueueName;
        readonly string connectionString;
        readonly string topicName;
        readonly int maxSizeInMB;
        readonly bool enablePartitioning;
        readonly Func<string, string> subscriptionShortener;

        public QueueCreator(string mainInputQueueName, string topicName, string connectionString, int maxSizeInMB, bool enablePartitioning, Func<string, string> subscriptionShortener)
        {
            this.mainInputQueueName = mainInputQueueName;
            this.connectionString = connectionString;
            this.topicName = topicName;
            this.maxSizeInMB = maxSizeInMB;
            this.enablePartitioning = enablePartitioning;
            this.subscriptionShortener = subscriptionShortener;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var client = new ManagementClient(connectionString);

            var topic = new TopicDescription(topicName)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = enablePartitioning,
                MaxSizeInMB = maxSizeInMB
            };

            await client.CreateTopicAsync(topic).ConfigureAwait(false);

            foreach (var address in queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses))
            {
                var queue = new QueueDescription(address)
                {
                    EnableBatchedOperations = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = int.MaxValue,
                    MaxSizeInMB = maxSizeInMB,
                    EnablePartitioning = enablePartitioning
                };

                await client.CreateQueueAsync(queue).ConfigureAwait(false);
            }

            var subscriptionName = subscriptionShortener(mainInputQueueName);
            var subscription = new SubscriptionDescription(topicName, subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = mainInputQueueName,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                // TODO: uncomment when https://github.com/Azure/azure-service-bus-dotnet/issues/499 is fixed
                //EnableBatchedOperations = true,
                // TODO: https://github.com/Azure/azure-service-bus-dotnet/issues/501 is fixed
                //UserMetadata = mainInputQueueName
            };

            await client.CreateSubscriptionAsync(subscription).ConfigureAwait(false);

            await client.DeleteRuleAsync(topicName, subscription.SubscriptionName, RuleDescription.DefaultRuleName).ConfigureAwait(false);
            
            await client.CloseAsync().ConfigureAwait(false);
        }
    }
}