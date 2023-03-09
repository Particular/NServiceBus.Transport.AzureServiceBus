namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
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
        readonly ServiceBusAdministrationClient administrativeClient;
        readonly NamespacePermissions namespacePermissions;
        readonly ServiceBusClient serviceBusClient;
        readonly string subscribingQueue;
        readonly string subscriptionName;

        public SubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            ServiceBusAdministrationClient administrativeClient,
            ServiceBusClient serviceBusClient,
            NamespacePermissions namespacePermissions)
        {
            this.subscribingQueue = subscribingQueue;
            this.transportSettings = transportSettings;
            this.administrativeClient = administrativeClient;
            this.serviceBusClient = serviceBusClient;
            this.namespacePermissions = namespacePermissions;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            var ruleManager = serviceBusClient.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
            await using (ruleManager.ConfigureAwait(false))
            {
                foreach (var eventType in eventTypes)
                {
                    await SubscribeEvent(ruleManager, eventType.MessageType, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        async Task SubscribeEvent(ServiceBusRuleManager ruleManager, Type eventType,
            CancellationToken cancellationToken)
        {
            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType);
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName}%'";

            try
            {
                // on an entity with forwarding enabled it is not possible to do GetRules
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

            var ruleManager = serviceBusClient.CreateRuleManager(transportSettings.Topology.TopicToSubscribeOn, subscriptionName);
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

        public async Task CreateSubscription(CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

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
                await administrativeClient.CreateSubscriptionAsync(subscription,
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