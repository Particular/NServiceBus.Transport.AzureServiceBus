namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;

    class SubscriptionManager : IManageSubscriptions
    {
        const int maxNameLength = 50;

        readonly string topicPath;
        readonly string connectionString;
        readonly Func<string, string> ruleShortener;
        readonly string subscriptionName;

        public SubscriptionManager(string inputQueueName, string topicPath, string connectionString, Func<string, string> subscriptionShortener, Func<string, string> ruleShortener)
        {
            this.topicPath = topicPath;
            this.connectionString = connectionString;
            this.ruleShortener = ruleShortener;

            subscriptionName = inputQueueName.Length > maxNameLength ? subscriptionShortener(inputQueueName) : inputQueueName;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            var ruleName = eventType.FullName.Length > maxNameLength ? ruleShortener(eventType.FullName) : eventType.FullName;
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.FullName},%'";
            var rule = new RuleDescription(ruleName, new SqlFilter(sqlExpression));
            
            var client = new SubscriptionClient(connectionString, topicPath, subscriptionName);

            // TODO: replace with management client to get a specific rule
            var rules = await client.GetRulesAsync().ConfigureAwait(false);
            
            var existingRule = rules.FirstOrDefault(x => x.Name == rule.Name);

            var ruleWasRemoved = false;

            if (existingRule != null && existingRule.Filter.ToString() != rule.Filter.ToString())
            {
                await client.RemoveRuleAsync(rule.Name).ConfigureAwait(false);
                ruleWasRemoved = true;
            }

            if (existingRule == null || ruleWasRemoved)
            {
                try
                {
                    await client.AddRuleAsync(rule).ConfigureAwait(false);
                }
                catch (ServiceBusException exception) when (exception.Message.Contains("already exists"))
                {
                }
            }

            await client.CloseAsync().ConfigureAwait(false);
        }

        public async Task Unsubscribe(Type eventType, ContextBag context)
        {
            var ruleName = eventType.FullName.Length > maxNameLength ? ruleShortener(eventType.FullName) : eventType.FullName;

            var client = new SubscriptionClient(connectionString, topicPath, subscriptionName);

            try
            {
                await client.RemoveRuleAsync(ruleName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }

            await client.CloseAsync().ConfigureAwait(false);
        }
    }
}