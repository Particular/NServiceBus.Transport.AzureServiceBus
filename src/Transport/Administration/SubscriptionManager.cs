namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Extensibility;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusAdministrationClient administrativeClient;
        readonly NamespacePermissions namespacePermissions;
        readonly string subscribingQueue;
        readonly string subscriptionName;

        public SubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            ServiceBusAdministrationClient administrativeClient,
            NamespacePermissions namespacePermissions)
        {
            this.subscribingQueue = subscribingQueue;
            this.transportSettings = transportSettings;
            this.administrativeClient = administrativeClient;
            this.namespacePermissions = namespacePermissions;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            foreach (var eventType in eventTypes)
            {
                await SubscribeEvent(administrativeClient, eventType.MessageType, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task SubscribeEvent(ServiceBusAdministrationClient client, Type eventType, CancellationToken cancellationToken)
        {
            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType);
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName}%'";
            var rule = new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlExpression));

            try
            {
                var existingRule = await client.GetRuleAsync(transportSettings.TopicName, subscriptionName, rule.Name, cancellationToken).ConfigureAwait(false);

                if (existingRule.Value.Filter.ToString() != rule.Filter.ToString())
                {
                    existingRule.Value.Action = rule.Action;

                    await client.UpdateRuleAsync(transportSettings.TopicName, subscriptionName, existingRule, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                try
                {
                    await client.CreateRuleAsync(transportSettings.TopicName, subscriptionName, rule, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                }
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType.MessageType);

            try
            {
                await administrativeClient.DeleteRuleAsync(transportSettings.TopicName, subscriptionName, ruleName, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        public async Task CreateSubscription(CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            var subscription = new CreateSubscriptionOptions(transportSettings.TopicName, subscriptionName)
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
            }
            // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
            catch (ServiceBusException sbe) when (
                sbe.IsTransient) //when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
            {
            }

        }
    }
}