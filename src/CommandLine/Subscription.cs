namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Subscription
    {
        public static Task CreateWithRejectAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            topicNameToUse = topicNameToUse.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            subscriptionNameToUse = subscriptionNameToUse.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.Value
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new FalseRuleFilter()));
        }

        public static Task CreateWithMatchAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            subscriptionNameToUse = subscriptionNameToUse.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.Value
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task CreateForwarding(ServiceBusAdministrationClient client, CommandOption topicToPublishTo, CommandOption topicToSubscribeTo, string subscriptionName, CommandOption hierarchyNamespace)
        {
            var options = new CreateSubscriptionOptions(topicToPublishTo.ToHierarchyNamespaceAwareDestination(hierarchyNamespace), subscriptionName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace))
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = topicToSubscribeTo.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = topicToSubscribeTo.Value()
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument endpointName,
            CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            subscriptionNameToUse = subscriptionNameToUse.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            return client.DeleteSubscriptionAsync(topicNameToUse, subscriptionNameToUse);
        }
    }
}