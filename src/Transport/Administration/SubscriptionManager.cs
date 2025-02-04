namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Extensibility;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        static readonly ILog Logger = LogManager.GetLogger<SubscriptionManager>();

        readonly bool setupInfrastructure;
        readonly ServiceBusAdministrationClient administrationClient;
        readonly string subscribingQueue;
        readonly AzureServiceBusTransport transportSettings;

        public SubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            bool setupInfrastructure,
            ServiceBusAdministrationClient administrationClient)
        {
            this.subscribingQueue = subscribingQueue;
            this.setupInfrastructure = setupInfrastructure;
            this.administrationClient = administrationClient;

            this.transportSettings = transportSettings;
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            if (eventTypes.Length == 0)
            {
                return;
            }

            if (eventTypes.Length == 1)
            {
                await SubscribeEvent(eventTypes[0], cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var subscribeTasks = new List<Task>(eventTypes.Length);
                foreach (var eventType in eventTypes)
                {
                    subscribeTasks.Add(SubscribeEvent(eventType, cancellationToken));
                }
                await Task.WhenAll(subscribeTasks)
                    .ConfigureAwait(false);
            }
        }

        async Task SubscribeEvent(MessageMetadata messageMetadata, CancellationToken cancellationToken)
        {
            var subscriptionInfos = transportSettings.Topology.GetSubscribeDestinations(messageMetadata.MessageType, subscribingQueue);
            var subscribeTasks = new List<Task>(subscriptionInfos.Length);
            foreach (var subscriptionInfo in subscriptionInfos)
            {
                subscribeTasks.Add(CreateSubscription(administrationClient, subscriptionInfo, cancellationToken));
            }
            await Task.WhenAll(subscribeTasks).ConfigureAwait(false);
        }

        async Task CreateSubscription(ServiceBusAdministrationClient client, SubscriptionInfo subscriptionInfo, CancellationToken cancellationToken)
        {
            if (setupInfrastructure)
            {
                var topicOptions = new CreateTopicOptions(subscriptionInfo.Topic)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes
                };

                try
                {
                    await client.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    // ignored due to race conditions
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Topic creation for topic {topicOptions.Name} is already in progress");
                }
                catch (UnauthorizedAccessException unauthorizedAccessException)
                {
                    // TODO: Check the log level
                    Logger.WarnFormat("Topic {0} could not be created. Reason: {1}", topicOptions.Name, unauthorizedAccessException.Message);
                    throw;
                }
            }

            var subscriptionOptions = new CreateSubscriptionOptions(subscriptionInfo.Topic, subscriptionInfo.SubscriptionName)
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
                await client.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // ignored due to race conditions
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Default subscription creation for topic {subscriptionOptions.TopicName} is already in progress");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                // TODO: Check the log level
                Logger.WarnFormat("Subscription {0} could not be created. Reason: {1}", subscriptionInfo.SubscriptionName, unauthorizedAccessException.Message);
                throw;
            }

            if (subscriptionInfo.RuleName is not null)
            {
                // Previously we used the rule manager here too
                // var ruleManager = client.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
                // await using (ruleManager.ConfigureAwait(false))
                // {
                try
                {
                    await administrationClient.CreateRuleAsync(subscriptionInfo.Topic, subscriptionInfo.SubscriptionName, new CreateRuleOptions(subscriptionInfo.RuleName, new SqlRuleFilter(subscriptionInfo.RuleFilter)), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    // ignored due to race conditions
                }
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            var subscriptionInfos = transportSettings.Topology.GetSubscribeDestinations(eventType.MessageType, subscribingQueue);
            var unsubscribeTasks = new List<Task>(subscriptionInfos.Length);
            foreach (var subscriptionInfo in subscriptionInfos)
            {
                unsubscribeTasks.Add(DeleteSubscription(subscriptionInfo));
            }
            await Task.WhenAll(unsubscribeTasks).ConfigureAwait(false);
            return;

            async Task DeleteSubscription(SubscriptionInfo subscriptionInfo)
            {
                if (subscriptionInfo.RuleName is not null)
                {
                    try
                    {
                        await administrationClient.DeleteRuleAsync(subscriptionInfo.Topic, subscriptionInfo.SubscriptionName, subscriptionInfo.RuleName, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                    {
                    }

                    // var ruleManager = administrationClient.CreateRuleManager(topicInfo.Topic, subscriptionName);
                    // await using (ruleManager.ConfigureAwait(false))
                    // {
                    //     try
                    //     {
                    //         await ruleManager.DeleteRuleAsync(ruleName, cancellationToken).ConfigureAwait(false);
                    //     }
                    //     catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                    //     {
                    //     }
                    // }
                }
                else
                {
                    try
                    {
                        await administrationClient.DeleteSubscriptionAsync(subscriptionInfo.Topic, subscriptionInfo.SubscriptionName, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                    {
                    }
                    catch (UnauthorizedAccessException unauthorizedAccessException)
                    {
                        Logger.InfoFormat("Subscription {0} could not be deleted. Reason: {1}", subscriptionInfo.SubscriptionName, unauthorizedAccessException.Message);
                    }
                }
            }
        }

        // TODO Let's double check if this is still needed because we assume we require manage rights now during subscription anyway
        // in the new topology. Or should we maybe keep in the migration topology things consistent in terms of manage rights?
        public async Task CreateSubscription(ServiceBusAdministrationClient adminClient, CancellationToken cancellationToken = default)
        {
            if (transportSettings.Topology is MigrationTopology migrationTopology)
            {
                var subscriptionName = migrationTopology.Options.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueue, subscribingQueue);

                var subscription = new CreateSubscriptionOptions(migrationTopology.TopicToSubscribeOn, subscriptionName)
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
                    await adminClient.CreateSubscriptionAsync(subscription,
                        new CreateRuleOptions("$default", new FalseRuleFilter()), cancellationToken).ConfigureAwait(false);
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
        }
    }
}