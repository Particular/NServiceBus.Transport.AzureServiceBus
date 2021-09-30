namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.Logging;

    class QueueCreator : ICreateQueues
    {
        const int maxNameLength = 50;

        readonly string mainInputQueueName;
        readonly string topicName;
        readonly ServiceBusAdministrationClient administrativeClient;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMB;
        readonly bool enablePartitioning;
        readonly Func<string, string> subscriptionShortener;
        readonly Func<string, string> subscriptionNamingConvention;
        static readonly ILog logger = LogManager.GetLogger<QueueCreator>();

        public QueueCreator(string mainInputQueueName, string topicName,
            ServiceBusAdministrationClient administrativeClient,
            NamespacePermissions namespacePermissions,
            int maxSizeInMB,
            bool enablePartitioning,
            Func<string, string> subscriptionShortener,
            Func<string, string> subscriptionNamingConvention)
        {
            this.mainInputQueueName = mainInputQueueName;
            this.topicName = topicName;
            this.administrativeClient = administrativeClient;
            this.namespacePermissions = namespacePermissions;
            this.maxSizeInMB = maxSizeInMB;
            this.enablePartitioning = enablePartitioning;
            this.subscriptionShortener = subscriptionShortener;
            this.subscriptionNamingConvention = subscriptionNamingConvention;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var result = await namespacePermissions.CanManage().ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new Exception(result.ErrorMessage);
            }

            var topic = new CreateTopicOptions(topicName)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = enablePartitioning,
                MaxSizeInMegabytes = maxSizeInMB
            };

            try
            {
                await administrativeClient.CreateTopicAsync(topic).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)
            {
                logger.Info($"Topic {topicName} creation already in progress.");
            }

            foreach (var address in queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses))
            {
                var queue = new CreateQueueOptions(address)
                {
                    EnableBatchedOperations = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = int.MaxValue,
                    MaxSizeInMegabytes = maxSizeInMB,
                    EnablePartitioning = enablePartitioning
                };

                try
                {
                    await administrativeClient.CreateQueueAsync(queue).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason ==
                                                      ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)
                {
                    logger.Info($"Queue {address} creation already in progress.");
                }
            }

            var subscriptionName = subscriptionNamingConvention(mainInputQueueName);
            subscriptionName = subscriptionName.Length > maxNameLength ? subscriptionShortener(subscriptionName) : subscriptionName;

            var subscription = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = mainInputQueueName,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = mainInputQueueName
            };

            try
            {
                await administrativeClient.CreateSubscriptionAsync(subscription, new CreateRuleOptions("$default", new FalseRuleFilter())).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)
            {
                logger.Info($"Subscription creation already in progress.");
            }
        }
    }
}