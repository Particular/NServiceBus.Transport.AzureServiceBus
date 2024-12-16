#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.Logging;

    class ForwardingTopologyCreator(AzureServiceBusTransport transportSettings)
    {
        static readonly ILog Logger = LogManager.GetLogger<ForwardingTopologyCreator>();

        public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues, CancellationToken cancellationToken = default)
        {
            var topology = transportSettings.Topology;
            var topicToPublishTo = new CreateTopicOptions(topology.TopicToPublishTo)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = transportSettings.EnablePartitioning,
                MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes
            };

            try
            {
                await adminClient.CreateTopicAsync(topicToPublishTo, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Logger.Info($"Topic {topicToPublishTo.Name} already exists");
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Topic creation for {topicToPublishTo.Name} is already in progress");
            }

            if (topology.IsHierarchy)
            {
                var topicToSubscribeOn = new CreateTopicOptions(topology.TopicToSubscribeOn)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes,
                };

                try
                {
                    await adminClient.CreateTopicAsync(topicToSubscribeOn, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Logger.Info($"Topic {topicToSubscribeOn.Name} already exists");
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Topic creation for {topicToSubscribeOn.Name} is already in progress");
                }

                var subscription = new CreateSubscriptionOptions(topology.TopicToPublishTo, $"forwardTo-{topology.TopicToSubscribeOn}")
                {
                    LockDuration = TimeSpan.FromMinutes(5),
                    ForwardTo = topology.TopicToSubscribeOn,
                    EnableDeadLetteringOnFilterEvaluationExceptions = false,
                    MaxDeliveryCount = int.MaxValue,
                    EnableBatchedOperations = true,
                    UserMetadata = topology.TopicToSubscribeOn
                };

                try
                {
                    await adminClient.CreateSubscriptionAsync(subscription,
                        new CreateRuleOptions("$default", new TrueRuleFilter()), cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Logger.Debug($"Default subscription rule for topic {subscription.TopicName} already exists");
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Default subscription rule for topic {subscription.TopicName} is already in progress");
                }
            }

            var queueCreator = new QueueCreator(transportSettings);
            await queueCreator.Create(adminClient, queues, cancellationToken).ConfigureAwait(false);
        }
    }
}