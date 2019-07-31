namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;

    static class Rule
    {
        public static Task Create(ManagementClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            var eventToSubscribeTo = eventType.Value;
            var ruleNameToUse = ruleName.HasValue() ? ruleName.Value() : eventToSubscribeTo;
            var description = new RuleDescription(ruleNameToUse, new SqlFilter($"[NServiceBus.EnclosedMessageTypes] LIKE '%{eventToSubscribeTo}%'"));

            return client.CreateRuleAsync(topicNameToUse, subscriptionNameToUse, description);
        }

        public static Task Delete(ManagementClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            var eventToSubscribeTo = eventType.Value;
            var ruleNameToUse = ruleName.HasValue() ? ruleName.Value() : eventToSubscribeTo;

            return client.DeleteRuleAsync(topicNameToUse, subscriptionNameToUse, ruleNameToUse);
        }
    }
}