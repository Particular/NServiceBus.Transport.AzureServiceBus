namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Subscription
    {
        public static Task CreateWithRejectAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName)
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

        public static Task CreateWithMatchAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandArgument topicName, CommandOption subscriptionName)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var options = new CreateSubscriptionOptions(topicName.Value, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.Value,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.Value
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task CreateForwarding(ServiceBusAdministrationClient client, CommandOption topicToPublishTo, CommandOption topicToSubscribeTo, string subscriptionName)
        {
            var options = new CreateSubscriptionOptions(topicToPublishTo.Value(), subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = topicToSubscribeTo.Value(),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = topicToSubscribeTo.Value()
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument endpointName,
            CommandArgument topicName, CommandOption subscriptionName)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            return client.DeleteSubscriptionAsync(topicName.Value, subscriptionNameToUse);
        }
    }
}