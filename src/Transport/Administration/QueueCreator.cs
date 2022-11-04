﻿namespace NServiceBus.Transport.AzureServiceBus
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

            var topicToPublishTo = new CreateTopicOptions(transportSettings.TopicNameToPublishTo)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = transportSettings.EnablePartitioning,
                MaxSizeInMegabytes = maxSizeInMb
            };

            try
            {
                await administrativeClient.CreateTopicAsync(topicToPublishTo, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Logger.Info($"Topic {topicToPublishTo.Name} already exists");
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Topic creation for {topicToPublishTo.Name} is already in progress");
            }

            // TODO: Comparison?
            if (transportSettings.TopicNameToPublishTo != transportSettings.TopicNameToSubscribeOn)
            {
                var topicToSubscribeOn = new CreateTopicOptions(transportSettings.TopicNameToSubscribeOn)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMegabytes = maxSizeInMb,
                };

                try
                {
                    await administrativeClient.CreateTopicAsync(topicToSubscribeOn, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Logger.Info($"Topic {topicToSubscribeOn.Name} already exists");
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Topic creation for {topicToSubscribeOn.Name} is already in progress");
                }

                // TODO: Apply naming convention?
                var subscription = new CreateSubscriptionOptions(transportSettings.TopicNameToPublishTo, $"ForwardTo-{transportSettings.TopicNameToSubscribeOn}")
                {
                    LockDuration = TimeSpan.FromMinutes(5),
                    ForwardTo = transportSettings.TopicNameToSubscribeOn,
                    EnableDeadLetteringOnFilterEvaluationExceptions = false,
                    MaxDeliveryCount = int.MaxValue,
                    EnableBatchedOperations = true,
                    UserMetadata = transportSettings.TopicNameToSubscribeOn
                };

                try
                {
                    await administrativeClient.CreateSubscriptionAsync(subscription,
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