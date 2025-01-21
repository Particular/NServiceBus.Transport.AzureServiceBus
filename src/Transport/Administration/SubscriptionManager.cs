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
        readonly EventRouting eventRouting;

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
            // Maybe this should be passed in so that the dispatcher and the subscription manager share the same cache?
            eventRouting = new EventRouting(this.transportSettings.Topology.Options);
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
            // TODO: There is no convention nor mapping here currently.
            // TODO: Is it a good idea to use the subscriptionName as the endpoint name?

            var topicsToSubscribeOn = eventRouting.GetSubscribeDestinations(messageMetadata.MessageType);
            var subscribeTasks = new List<Task>(topicsToSubscribeOn.Length);
            foreach (var topicInfo in topicsToSubscribeOn)
            {
                subscribeTasks.Add(CreateSubscription(administrationClient, topicInfo, messageMetadata, cancellationToken));
            }
            await Task.WhenAll(subscribeTasks).ConfigureAwait(false);
        }

        async Task CreateSubscription(ServiceBusAdministrationClient client, (string Topic, bool RequiresRule) topicInfo, MessageMetadata messageMetadata, CancellationToken cancellationToken)
        {
            if (setupInfrastructure)
            {
                var topicOptions = new CreateTopicOptions(topicInfo.Topic)
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

            var subscriptionName = eventRouting.GetSubscriptionName(subscribingQueue);
            var subscriptionOptions = new CreateSubscriptionOptions(topicInfo.Topic, subscriptionName)
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
                Logger.WarnFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                throw;
            }

            if (topicInfo.RequiresRule)
            {
                var ruleName = eventRouting.GetSubscriptionRuleName(messageMetadata.MessageType);
                var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{messageMetadata.MessageType.FullName}%'";

                // Previously we used the rule manager here too
                // var ruleManager = client.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
                // await using (ruleManager.ConfigureAwait(false))
                // {
                try
                {
                    await administrationClient.CreateRuleAsync(topicInfo.Topic, subscriptionName, new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlExpression)), cancellationToken)
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
            var topicsToUnsubscribeOn = eventRouting.GetSubscribeDestinations(eventType.MessageType);
            var unsubscribeTasks = new List<Task>(topicsToUnsubscribeOn.Length);
            foreach (var topicInfo in topicsToUnsubscribeOn)
            {
                unsubscribeTasks.Add(DeleteSubscription(topicInfo));
            }
            await Task.WhenAll(unsubscribeTasks).ConfigureAwait(false);
            return;

            async Task DeleteSubscription((string Topic, bool RequiresRule) topicInfo)
            {
                var subscriptionName = eventRouting.GetSubscriptionName(subscribingQueue);

                if (topicInfo.RequiresRule)
                {
                    var ruleName = eventRouting.GetSubscriptionRuleName(eventType.MessageType);

                    try
                    {
                        await administrationClient.DeleteRuleAsync(topicInfo.Topic, subscriptionName, ruleName, cancellationToken).ConfigureAwait(false);
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
                        await administrationClient.DeleteSubscriptionAsync(topicInfo.Topic, subscriptionName, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                    {
                    }
                    catch (UnauthorizedAccessException unauthorizedAccessException)
                    {
                        Logger.InfoFormat("Subscription {0} could not be deleted. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                    }
                }
            }
        }

        public async Task CreateSubscription(ServiceBusAdministrationClient adminClient, CancellationToken cancellationToken = default)
        {
            if (transportSettings.Topology is MigrationTopology migrationTopology)
            {
                var subscriptionName = eventRouting.GetSubscriptionName(subscribingQueue);

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