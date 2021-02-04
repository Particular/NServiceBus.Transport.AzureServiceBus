namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Extensibility;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        readonly string subscribingQueue;
        readonly string subscriptionName;

        public SubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            NamespacePermissions namespacePermissions)
        {
            this.subscribingQueue = subscribingQueue;
            this.transportSettings = transportSettings;
            this.connectionStringBuilder = connectionStringBuilder;
            this.namespacePermissions = namespacePermissions;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context)
        {
            await CheckForManagePermissions().ConfigureAwait(false);
            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                foreach (var eventType in eventTypes)
                {
                    await SubscribeEvent(client, eventType.MessageType).ConfigureAwait(false);
                }
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }

        async Task SubscribeEvent(ManagementClient client, Type eventType)
        {
            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType);
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName}%'";
            var rule = new RuleDescription(ruleName, new SqlFilter(sqlExpression));

            try
            {
                var existingRule = await client.GetRuleAsync(transportSettings.TopicName, subscriptionName, rule.Name).ConfigureAwait(false);

                if (existingRule.Filter.ToString() != rule.Filter.ToString())
                {
                    rule.Action = existingRule.Action;

                    await client.UpdateRuleAsync(transportSettings.TopicName, subscriptionName, rule).ConfigureAwait(false);
                }
            }
            catch (MessagingEntityNotFoundException)
            {
                try
                {
                    await client.CreateRuleAsync(transportSettings.TopicName, subscriptionName, rule).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context)
        {
            await CheckForManagePermissions().ConfigureAwait(false);

            var ruleName = transportSettings.SubscriptionRuleNamingConvention(eventType.MessageType);

            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                await client.DeleteRuleAsync(transportSettings.TopicName, subscriptionName, ruleName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }

        public async Task CreateSubscription()
        {
            await namespacePermissions.CanManage().ConfigureAwait(false);

            var client = new ManagementClient(connectionStringBuilder, transportSettings.CustomTokenProvider);

            try
            {
                var subscription = new SubscriptionDescription(transportSettings.TopicName, subscriptionName)
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
                    await client.CreateSubscriptionAsync(subscription, new RuleDescription("$default", new FalseFilter())).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
                // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                {
                }
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }

        async Task CheckForManagePermissions()
        {
            if (!verifiedManagePermissions)
            {
                await namespacePermissions.CanManage().ConfigureAwait(false);
                verifiedManagePermissions = true;
            }
        }

        bool verifiedManagePermissions;
    }
}