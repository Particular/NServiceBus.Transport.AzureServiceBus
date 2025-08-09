namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;
using Particular.Obsoletes;

[ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
[Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
sealed class MigrationTopologyCreator(AzureServiceBusTransport transportSettings)
{
    static readonly ILog Logger = LogManager.GetLogger<TopologyCreator>();

    public async Task Create(ServiceBusAdministrationClient adminClient, CancellationToken cancellationToken = default)
    {
        if (transportSettings.Topology is MigrationTopology migrationTopology)
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