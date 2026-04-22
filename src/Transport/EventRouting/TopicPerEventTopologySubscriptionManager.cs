namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using EventRouting;
using Extensibility;
using Logging;
using Unicast.Messages;

sealed class TopicPerEventTopologySubscriptionManager : SubscriptionManager
{
    readonly TopologyOptions topologyOptions;
    readonly StartupDiagnosticEntries startupDiagnostic;
    readonly string subscriptionName;
    readonly DestinationManager destinationManager;

    public TopicPerEventTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions,
        TopologyOptions topologyOptions,
        StartupDiagnosticEntries startupDiagnostic) : base(creationOptions)
    {
        this.topologyOptions = topologyOptions;
        this.startupDiagnostic = startupDiagnostic;
        // The subscription name is limited to 50 characters and the hierarchy is respected by the topic name
        // so there is no need to add it to the subscription name.
        destinationManager = new DestinationManager(topologyOptions.HierarchyNamespaceOptions);
        var subscribingQueueName = CreationOptions.SubscribingQueueName;
        var strippedSubscribingQueueName = destinationManager.StripHierarchyNamespace(subscribingQueueName);

        var subscriptionNameCandidate =
            topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueName)
            ?? topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(strippedSubscribingQueueName)
            ?? subscribingQueueName;

        subscriptionName = destinationManager.StripHierarchyNamespace(subscriptionNameCandidate);
    }

    static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTopologySubscriptionManager>();

    public override Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        WriteSubscriptionManifest(eventTypes);

        var invalidConfig = ValidateSubscriptionConfiguration(eventTypes);
        if (invalidConfig != null)
        {
            throw new InvalidOperationException(invalidConfig);
        }

        return eventTypes.Length switch
        {
            0 => Task.CompletedTask,
            1 => SubscribeEvent(eventTypes[0].MessageType.FullName!, cancellationToken),
            _ => Task.WhenAll([.. eventTypes.Select(eventType =>
                    SubscribeEvent(eventType.MessageType.FullName!, cancellationToken))])
        };
    }

    string? ValidateSubscriptionConfiguration(MessageMetadata[] eventTypes)
    {
        var topicRoutingModes = new Dictionary<string, TopicRoutingMode>(StringComparer.OrdinalIgnoreCase);

        foreach (var eventType in eventTypes)
        {
            var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            var entries = MapEventToSubscriptionEntries(eventTypeFullName);

            foreach (var entry in entries)
            {
                var topicName = entry.Topic;
                var effectiveRoutingMode = NormalizeSubscriptionRoutingMode(ResolveTopicRoutingMode(entry.RoutingMode));
                if (topicRoutingModes.TryGetValue(topicName, out var existingMode))
                {
                    if (existingMode != effectiveRoutingMode)
                    {
                        return $"Incompatible subscription routing modes detected for topic '{topicName}' on subscription '{subscriptionName}'. Event '{eventTypeFullName}' uses '{effectiveRoutingMode}' but other events use '{existingMode}'. All events subscribed to the same topic must use the same routing mode on the same endpoint subscription.";
                    }
                }
                else
                {
                    topicRoutingModes[topicName] = effectiveRoutingMode;
                }
            }
        }

        return null;
    }

    void WriteSubscriptionManifest(MessageMetadata[] eventTypes)
    {
        var subscriptions = eventTypes
            .Select(eventType => eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"))
            .SelectMany(eventTypeFullName => MapEventToSubscriptionEntries(eventTypeFullName)
                .Select(entry => new { Topic = destinationManager.GetDestination(entry.Topic, eventTypeFullName).ToLower(), entry.RoutingMode, MessageType = eventTypeFullName }))
            .GroupBy(topicAndMessageType => (topicAndMessageType.Topic, topicAndMessageType.RoutingMode))
            .Select(group => new
            {
                TopicName = group.Key.Topic,
                RoutingMode = group.Key.RoutingMode.ToString(),
                MessageTypes = group.Select(x => x.MessageType).ToArray()
            })
            .ToArray();
        startupDiagnostic.Add("Manifest-Subscriptions", subscriptions);
    }

    Task SubscribeEvent(string eventTypeFullName, CancellationToken cancellationToken)
    {
        var entries = MapEventToSubscriptionEntries(eventTypeFullName);
        return CreateSubscriptionsForEntries(entries, eventTypeFullName, subscriptionName, CreationOptions, cancellationToken);
    }

    HashSet<SubscriptionEntry> MapEventToSubscriptionEntries(string eventTypeFullName)
    {
        var entries = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, GetFallbackOrDefaultEntries(eventTypeFullName));
        return [.. entries.Select(entry => entry with { Topic = destinationManager.GetDestination(entry.Topic, eventTypeFullName), RoutingMode = ResolveTopicRoutingMode(entry.RoutingMode) })];
    }

    HashSet<SubscriptionEntry> GetFallbackOrDefaultEntries(string eventTypeFullName)
    {
        if (topologyOptions.FallbackTopic?.TopicName is { Length: > 0 } fallbackTopicName)
        {
            return [new SubscriptionEntry(fallbackTopicName, topologyOptions.FallbackTopic.Mode)];
        }

        return [new SubscriptionEntry(eventTypeFullName, TopicRoutingMode.Default)];
    }

    TopicRoutingMode ResolveTopicRoutingMode(TopicRoutingMode routingMode)
    {
        if (routingMode != TopicRoutingMode.Default)
        {
            return routingMode;
        }

        return TopicRoutingMode.CatchAll;
    }

    static TopicRoutingMode NormalizeSubscriptionRoutingMode(TopicRoutingMode routingMode) =>
        routingMode switch
        {
            // A catch-all subscription already receives every message on the topic.
            // Combining it with a correlation-filtered rule is redundant, but Azure Service Bus
            // evaluates the rules using OR semantics, so the combination is harmless and should
            // not be rejected during startup validation.
            TopicRoutingMode.NotMultiplexed or TopicRoutingMode.CatchAll or TopicRoutingMode.CorrelationFilter => TopicRoutingMode.CatchAll,
            TopicRoutingMode.Default => TopicRoutingMode.Default,
            TopicRoutingMode.SqlFilter => TopicRoutingMode.SqlFilter,
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null)
        };

    public override Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        var entries = MapEventToSubscriptionEntries(eventTypeFullName);
        return DeleteRulesForEntries(entries, eventTypeFullName, subscriptionName, CreationOptions, cancellationToken);
    }

    public static Task CreateSubscriptionsForEntries(HashSet<SubscriptionEntry> entries, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll([.. entries.Select(entry => CreateSubscriptionForEntry(entry, eventTypeFullName, subscriptionName, creationOptions, cancellationToken))]);
    }

    static async Task CreateSubscriptionForEntry(SubscriptionEntry entry, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var topicName = entry.Topic;

        if (creationOptions.SetupInfrastructure)
        {
            var topicOptions = new CreateTopicOptions(topicName)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = creationOptions.EnablePartitioning,
                MaxSizeInMegabytes = creationOptions.EntityMaximumSizeInMegabytes
            };

            try
            {
                await creationOptions.AdministrationClient.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)
            {
                Logger.Info($"Topic creation for topic {topicOptions.Name} is already in progress");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.ErrorFormat("Topic {0} could not be created. Reason: {1}", topicOptions.Name, unauthorizedAccessException.Message);
                throw;
            }
        }

        switch (entry.RoutingMode)
        {
            case TopicRoutingMode.NotMultiplexed:
            case TopicRoutingMode.CatchAll:
                await CreateCatchAllSubscription(topicName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false);
                break;
            case TopicRoutingMode.CorrelationFilter:
            case TopicRoutingMode.SqlFilter:
                await CreateFilteredSubscription(topicName, subscriptionName, eventTypeFullName, entry.RoutingMode, creationOptions, cancellationToken).ConfigureAwait(false);
                break;
            case TopicRoutingMode.Default:
            default:
                throw new ArgumentOutOfRangeException(nameof(entry.RoutingMode), entry.RoutingMode, "Unknown routing mode");
        }
    }

    static async Task CreateCatchAllSubscription(string topicName, string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            LockDuration = TimeSpan.FromMinutes(5),
            ForwardTo = creationOptions.SubscribingQueueName,
            EnableDeadLetteringOnFilterEvaluationExceptions = false,
            MaxDeliveryCount = creationOptions.MaxDeliveryCount,
            EnableBatchedOperations = true,
            UserMetadata = creationOptions.SubscribingQueueName
        };

        try
        {
            await creationOptions.AdministrationClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
        catch (ServiceBusException sbe) when (sbe.IsTransient)
        {
            Logger.Info($"Default subscription creation for topic {subscriptionOptions.TopicName} is already in progress");
        }
        catch (UnauthorizedAccessException unauthorizedAccessException)
        {
            Logger.ErrorFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
            throw;
        }
    }

    static async Task CreateFilteredSubscription(string topicName, string subscriptionName, string eventTypeFullName,
        TopicRoutingMode routingMode,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            LockDuration = TimeSpan.FromMinutes(5),
            ForwardTo = creationOptions.SubscribingQueueName,
            EnableDeadLetteringOnFilterEvaluationExceptions = false,
            MaxDeliveryCount = creationOptions.MaxDeliveryCount,
            EnableBatchedOperations = true,
            UserMetadata = creationOptions.SubscribingQueueName
        };

        try
        {
            await creationOptions.AdministrationClient.CreateSubscriptionAsync(subscriptionOptions,
                new CreateRuleOptions("$default", new FalseRuleFilter()), cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
        catch (ServiceBusException sbe) when (sbe.IsTransient)
        {
            Logger.Info($"Subscription creation for topic {subscriptionOptions.TopicName} with FalseRuleFilter is already in progress");
        }
        catch (UnauthorizedAccessException unauthorizedAccessException)
        {
            Logger.ErrorFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
            throw;
        }

        var ruleManager = creationOptions.Client.CreateRuleManager(topicName, subscriptionName);
        await using (ruleManager.ConfigureAwait(false))
        {
            var ruleName = GetRuleName(eventTypeFullName);
            RuleFilter ruleFilter = routingMode == TopicRoutingMode.CorrelationFilter
                ? new CorrelationRuleFilter { ApplicationProperties = { [eventTypeFullName] = true } }
                : new SqlRuleFilter($"[{Headers.EnclosedMessageTypes}] LIKE '%{eventTypeFullName}%'");

            try
            {
                await ruleManager.CreateRuleAsync(new CreateRuleOptions(ruleName, ruleFilter), cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
        }
    }

    static string GetRuleName(string eventTypeFullName)
    {
        if (eventTypeFullName.Length <= 50)
        {
            return eventTypeFullName;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(eventTypeFullName));
        var hashString = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return $"Rule-{hashString}";
    }

    public static Task DeleteRulesForEntries(HashSet<SubscriptionEntry> entries, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll([.. entries.Select(entry => DeleteRuleForEntry(entry, eventTypeFullName, subscriptionName, creationOptions, cancellationToken))]);
    }

    static Task DeleteRuleForEntry(SubscriptionEntry entry, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        return entry.RoutingMode switch
        {
            TopicRoutingMode.NotMultiplexed => Task.CompletedTask,
            TopicRoutingMode.CatchAll => Task.CompletedTask,
            TopicRoutingMode.CorrelationFilter or TopicRoutingMode.SqlFilter =>
                DeleteRuleForFilteredSubscription(entry.Topic, eventTypeFullName, subscriptionName, creationOptions, cancellationToken),
            TopicRoutingMode.Default => Task.CompletedTask,
            _ => Task.CompletedTask
        };
    }

    static async Task DeleteRuleForFilteredSubscription(string topicName, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var ruleManager = creationOptions.Client.CreateRuleManager(topicName, subscriptionName);
        await using (ruleManager.ConfigureAwait(false))
        {
            var ruleName = GetRuleName(eventTypeFullName);
            try
            {
                await ruleManager.DeleteRuleAsync(ruleName, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }
    }

    public static Task CreateSubscriptionsForTopics(HashSet<string> topics,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll([.. topics.Select(topicName => CreateCatchAllSubscription(topicName, subscriptionName, creationOptions, cancellationToken))]);
    }

    public static Task DeleteSubscriptionsForTopics(HashSet<string> topics, string subscriptionName,
        ServiceBusAdministrationClient administrationClient,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll([.. topics.Select(topicName => DeleteSubscription(topicName, subscriptionName, administrationClient, cancellationToken))]);

        async Task DeleteSubscription(string topicName, string subName,
            ServiceBusAdministrationClient adminClient,
            CancellationToken token)
        {
            try
            {
                await adminClient.DeleteSubscriptionAsync(topicName, subName, token).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.InfoFormat("Subscription {0} could not be deleted. Reason: {1}", subName, unauthorizedAccessException.Message);
            }
        }
    }
}
