namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Extensibility;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        const int maxNameLength = 50;

        readonly string topicPath;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly ITokenProvider tokenProvider;
        readonly NamespacePermissions namespacePermissions;
        readonly Func<Type, string> subscriptionRuleNamingConvention;
        readonly string subscriptionName;

        StartupCheckResult startupCheckResult;

        public SubscriptionManager(string inputQueueName, string topicPath,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            ITokenProvider tokenProvider,
            NamespacePermissions namespacePermissions,
            Func<string, string> subscriptionNamingConvention,
            Func<Type, string> subscriptionRuleNamingConvention)
        {
            this.topicPath = topicPath;
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
            this.namespacePermissions = namespacePermissions;
            this.subscriptionRuleNamingConvention = subscriptionRuleNamingConvention;

            subscriptionName = subscriptionNamingConvention(inputQueueName);
        }

        public async Task Subscribe(MessageMetadata eventType, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            await CheckForManagePermissions().ConfigureAwait(false);

            var ruleName = subscriptionRuleNamingConvention(eventType.MessageType);
            var sqlExpression = $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventType.MessageType.FullName}%'";
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
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            await CheckForManagePermissions().ConfigureAwait(false);

            var ruleName = subscriptionRuleNamingConvention(eventType.MessageType);

            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                await client.DeleteRuleAsync(topicPath, subscriptionName, ruleName).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
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