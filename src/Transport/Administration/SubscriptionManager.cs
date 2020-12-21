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
        readonly NamespacePermissions namespacePermissions;
        readonly Func<string, string> ruleShortener;
        readonly Func<string, string> subscriptionRuleNamingConvention;
        readonly string subscriptionName;

        StartupCheckResult startupCheckResult;

        public SubscriptionManager(string inputQueueName, string topicPath,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            ITokenProvider tokenProvider,
            NamespacePermissions namespacePermissions,
            Func<string, string> subscriptionShortener,
            Func<string, string> ruleShortener,
            Func<string, string> subscriptionNamingConvention,
            Func<string, string> subscriptionRuleNamingConvention)
        {
            this.topicPath = topicPath;
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
            this.namespacePermissions = namespacePermissions;
            this.ruleShortener = ruleShortener;
            this.subscriptionRuleNamingConvention = subscriptionRuleNamingConvention;

            subscriptionName = subscriptionNamingConvention(inputQueueName);
            subscriptionName = subscriptionName.Length > maxNameLength ? subscriptionShortener(subscriptionName) : subscriptionName;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            await CheckForManagePermissions().ConfigureAwait(false);

            var ruleName = subscriptionRuleNamingConvention(eventType.FullName);
            ruleName = ruleName.Length > maxNameLength ? ruleShortener(ruleName) : ruleName;
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
            await CheckForManagePermissions().ConfigureAwait(false);

            var ruleName = subscriptionRuleNamingConvention(eventType.FullName);
            ruleName = ruleName.Length > maxNameLength ? ruleShortener(ruleName) : ruleName;

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

        async Task CheckForManagePermissions()
        {
            if (startupCheckResult == null)
            {
                startupCheckResult = await namespacePermissions.CanManage().ConfigureAwait(false);
            }

            if (!startupCheckResult.Succeeded)
            {
                throw new Exception(startupCheckResult.ErrorMessage);
            }
        }
    }
}