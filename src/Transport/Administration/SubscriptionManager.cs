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

        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusClient client;
        readonly string subscribingQueue;
        readonly string subscriptionName;

        public SubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            ServiceBusClient client)
        {
            this.subscribingQueue = subscribingQueue;
            this.transportSettings = transportSettings;
            this.client = client;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            if (eventTypes.Length == 0)
            {
                return;
            }

            var ruleManager = client.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
            await using (ruleManager.ConfigureAwait(false))
            {
                if (eventTypes.Length == 1)
                {
                    await SubscribeEvent(ruleManager, eventTypes[0].MessageType, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    var subscribeTasks = new List<Task>(eventTypes.Length);
                    foreach (var eventType in eventTypes)
                    {
                        subscribeTasks.Add(SubscribeEvent(ruleManager, eventType.MessageType, cancellationToken));
                    }
                    await Task.WhenAll(subscribeTasks)
                        .ConfigureAwait(false);
                }
            }
        }

        async Task SubscribeEvent(ServiceBusRuleManager ruleManager, Type eventType,
            CancellationToken cancellationToken)
        {
            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType);
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName}%'";

            // on an entity with forwarding enabled it is not possible to do GetRules so we first have to delete and then create
            // to preserve the update or create behavior

            try
            {
                await ruleManager.DeleteRuleAsync(ruleName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                // ignored due to race conditions
            }

            try
            {
                await ruleManager.CreateRuleAsync(new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlExpression)), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // ignored due to race conditions
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType.MessageType);

            var ruleManager = client.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
            await using (ruleManager.ConfigureAwait(false))
            {
                try
                {
                    await ruleManager.DeleteRuleAsync(ruleName, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                }
            }
        }

        public async Task CreateSubscription(ServiceBusAdministrationClient adminClient, CancellationToken cancellationToken = default)
        {
            var subscription = new CreateSubscriptionOptions(transportSettings.Topology.TopicToSubscribeOn, subscriptionName)
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