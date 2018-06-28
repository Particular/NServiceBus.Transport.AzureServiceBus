namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Microsoft.Azure.ServiceBus.Primitives;

    class SubscriptionManager : IManageSubscriptions
    {
        const int maxNameLength = 50;

        readonly string topicPath;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly ITokenProvider tokenProvider;
        readonly Func<string, string> ruleShortener;
        readonly string subscriptionName;

        public SubscriptionManager(string inputQueueName, string topicPath, ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider, Func<string, string> subscriptionShortener, Func<string, string> ruleShortener)
        {
            this.topicPath = topicPath;
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
            this.ruleShortener = ruleShortener;

            subscriptionName = inputQueueName.Length > maxNameLength ? subscriptionShortener(inputQueueName) : inputQueueName;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            var ruleName = eventType.FullName.Length > maxNameLength ? ruleShortener(eventType.FullName) : eventType.FullName;
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName}%'";
            var rule = new RuleDescription(ruleName, new SqlFilter(sqlExpression));

            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                var existingRule = await client.GetRuleAsync(topicPath, subscriptionName, rule.Name).ConfigureAwait(false);

                if (existingRule.Filter.ToString() != rule.Filter.ToString())
                {
                    rule.Action = existingRule.Action;

                    await client.UpdateRuleAsync(topicPath, subscriptionName, rule).ConfigureAwait(false);
                }
            }
            catch (MessagingEntityNotFoundException)
            {
                try
                {
                    await client.CreateRuleAsync(topicPath, subscriptionName, rule).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
            }

            await client.CloseAsync().ConfigureAwait(false);
        }

        public async Task Unsubscribe(Type eventType, ContextBag context)
        {
            var ruleName = eventType.FullName.Length > maxNameLength ? ruleShortener(eventType.FullName) : eventType.FullName;

            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                await client.DeleteRuleAsync(topicPath, subscriptionName, ruleName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }

            await client.CloseAsync().ConfigureAwait(false);
        }
    }
}