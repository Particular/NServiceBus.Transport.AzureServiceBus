namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;

    static class Rule
    {
        public static Task Delete(ManagementClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, string ruleName = RuleDescription.DefaultRuleName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            // TODO: remove when https://github.com/Azure/azure-service-bus-dotnet/issues/502 is implemented
            return client.DeleteRuleAsync(topicNameToUse, subscriptionNameToUse, ruleName);
        }
    }
}