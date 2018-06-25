namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    static class Subscription
    {
        public static Task Create(ManagementClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandOption<int> size, CommandOption partitioning)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : "bundle-1";
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var subscriptionDescription = new SubscriptionDescription(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.Value,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                // TODO: uncomment when https://github.com/Azure/azure-service-bus-dotnet/issues/499 is fixed
                //EnableBatchedOperations = true,
                // TODO: https://github.com/Azure/azure-service-bus-dotnet/issues/501 is fixed
                //UserMetadata = name.Value
            };

            return client.CreateSubscriptionAsync(subscriptionDescription);
        }
    }
}