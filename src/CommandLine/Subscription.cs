namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Subscription
    {
        public static Task CreateWithRejectAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandOption topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : Topic.DefaultTopicName;
            topicNameToUse = topicNameToUse.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace)
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new FalseRuleFilter()));
        }

        public static Task CreateWithMatchAll(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace)
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task CreateForwarding(ServiceBusAdministrationClient client, CommandOption topicToPublishTo, CommandOption topicToSubscribeOn, string subscriptionName, CommandOption hierarchyNamespace)
        {
            var options = new CreateSubscriptionOptions(topicToPublishTo.ToHierarchyNamespaceAwareDestination(hierarchyNamespace), subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = topicToSubscribeOn.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = topicToSubscribeOn.ToHierarchyNamespaceAwareDestination(hierarchyNamespace)
            };

            return client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new TrueRuleFilter()));
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument endpointName,
            CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            return client.DeleteSubscriptionAsync(topicNameToUse, subscriptionNameToUse);
        }

        public static async Task CreateWithFilteredRules(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace, List<string> eventTypes, bool useCorrelationFilter)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;

            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            var options = new CreateSubscriptionOptions(topicNameToUse, subscriptionNameToUse)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace),
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = endpointName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace)
            };

            try
            {
                await client.CreateSubscriptionAsync(options, new CreateRuleOptions("$default", new FalseRuleFilter())).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Console.WriteLine($"Subscription '{subscriptionNameToUse}' already exists, adding rules.");
            }

            foreach (var eventType in eventTypes)
            {
                var ruleName = GetRuleName(eventType);
                RuleFilter ruleFilter = useCorrelationFilter
                    ? new CorrelationRuleFilter { ApplicationProperties = { [eventType] = true } }
                    : new SqlRuleFilter($"[NServiceBus.EnclosedMessageTypes] LIKE '%{eventType}%'");

                try
                {
                    await client.CreateRuleAsync(topicNameToUse, subscriptionNameToUse, new CreateRuleOptions(ruleName, ruleFilter)).ConfigureAwait(false);
                    Console.WriteLine($"Rule '{ruleName}' added for event type '{eventType}'.");
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Rule '{ruleName}' already exists, skipping.");
                }
            }
        }

        public static async Task DeleteRules(ServiceBusAdministrationClient client, CommandArgument endpointName, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace, List<string> eventTypes)
        {
            var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : endpointName.Value;
            var topicNameToUse = topicName.ToHierarchyNamespaceAwareDestination(hierarchyNamespace);

            foreach (var eventType in eventTypes)
            {
                var ruleName = GetRuleName(eventType);

                try
                {
                    await client.DeleteRuleAsync(topicNameToUse, subscriptionNameToUse, ruleName).ConfigureAwait(false);
                    Console.WriteLine($"Rule '{ruleName}' deleted for event type '{eventType}'.");
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    Console.WriteLine($"Rule '{ruleName}' not found, skipping.");
                }
            }

            var remainingRules = new List<RuleProperties>();
            await foreach (var rule in client.GetRulesAsync(topicNameToUse, subscriptionNameToUse).ConfigureAwait(false))
            {
                remainingRules.Add(rule);
            }

            var hasOnlyDefaultRule = remainingRules.Count == 1 && remainingRules[0].Name == "$default";

            if (remainingRules.Count == 0 || hasOnlyDefaultRule)
            {
                await client.DeleteSubscriptionAsync(topicNameToUse, subscriptionNameToUse).ConfigureAwait(false);
                Console.WriteLine($"Subscription '{subscriptionNameToUse}' deleted (no rules remain).");
            }
        }

        static string GetRuleName(string eventTypeFullName)
        {
            if (eventTypeFullName.Length <= 50)
            {
                return eventTypeFullName;
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(eventTypeFullName));
            var hashString = Convert.ToHexString(hash)[..16].ToLowerInvariant();
            return $"Rule-{hashString}";
        }
    }
}