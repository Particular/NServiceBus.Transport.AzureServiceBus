namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    static class Subscription
    {
        public static Task Create(ManagementClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var subscriptionDescription = new SubscriptionDescription(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.Value,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.Value
            };

            return client.CreateSubscriptionAsync(subscriptionDescription);
        }
    }
}