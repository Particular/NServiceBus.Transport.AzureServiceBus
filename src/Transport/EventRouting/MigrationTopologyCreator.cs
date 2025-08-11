namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;

sealed class MigrationTopologyCreator(AzureServiceBusTransport transportSettings)
{
    static readonly ILog Logger = LogManager.GetLogger<TopologyCreator>();

    public async Task Create(ServiceBusAdministrationClient adminClient, CancellationToken cancellationToken = default)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        if (transportSettings.Topology is MigrationTopology migrationTopology)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var topicToPublishTo = new CreateTopicOptions(migrationTopology.TopicToPublishTo)
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
            catch (ServiceBusException sbe) when (sbe.IsTransient) // An operation is in progress.
            {
                Logger.Info($"Topic creation for {topicToPublishTo.Name} is already in progress");
            }

            if (migrationTopology.IsHierarchy)
            {
                var topicToSubscribeOn = new CreateTopicOptions(migrationTopology.TopicToSubscribeOn)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes,
                };

                try
                {
                    await adminClient.CreateTopicAsync(topicToSubscribeOn, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when
                    (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Logger.Info($"Topic {topicToSubscribeOn.Name} already exists");
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient) // An operation is in progress.
                {
                    Logger.Info($"Topic creation for {topicToSubscribeOn.Name} is already in progress");
                }

                var subscription =
                    new CreateSubscriptionOptions(migrationTopology.TopicToPublishTo,
                        $"forwardTo-{migrationTopology.TopicToSubscribeOn}")
                    {
                        LockDuration = TimeSpan.FromMinutes(5),
                        ForwardTo = migrationTopology.TopicToSubscribeOn,
                        EnableDeadLetteringOnFilterEvaluationExceptions = false,
                        MaxDeliveryCount = int.MaxValue,
                        EnableBatchedOperations = true,
                        UserMetadata = migrationTopology.TopicToSubscribeOn
                    };

                try
                {
                    await adminClient.CreateSubscriptionAsync(subscription,
                            new CreateRuleOptions("$default", new TrueRuleFilter()), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when
                    (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Default subscription rule for topic {subscription.TopicName} already exists");
                    }
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient) // An operation is in progress.
                {
                    Logger.Info($"Default subscription rule for topic {subscription.TopicName} is already in progress");
                }
            }
        }
    }
}