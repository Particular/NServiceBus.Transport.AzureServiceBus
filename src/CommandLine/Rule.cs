namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Rule
    {
        public static Task Create(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            var eventToSubscribeTo = eventType.Value;
            var ruleNameToUse = ruleName.HasValue() ? ruleName.Value() : eventToSubscribeTo;
            var description = new CreateRuleOptions(ruleNameToUse, new SqlRuleFilter($"[NServiceBus.EnclosedMessageTypes] LIKE '%{eventToSubscribeTo}%'"));

            return client.CreateRuleAsync(topicNameToUse, subscriptionNameToUse, description);
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            var eventToSubscribeTo = eventType.Value;
            var ruleNameToUse = ruleName.HasValue() ? ruleName.Value() : eventToSubscribeTo;

            return client.DeleteRuleAsync(topicNameToUse, subscriptionNameToUse, ruleNameToUse);
        }
    }
}