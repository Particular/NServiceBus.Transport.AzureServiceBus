namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Subscription
    {
        public static Task Create(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.Value,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.Value
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new FalseRuleFilter()));
        }
    }
}