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

        destinationManager = new DestinationManager(topologyOptions.HierarchyNamespaceOptions);
        var subscribingQueueNameWithoutHierarchy = destinationManager.StripHierarchyNamespace(CreationOptions.SubscribingQueueName);
        subscriptionName = topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(CreationOptions.SubscribingQueueName,
            topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueNameWithoutHierarchy, CreationOptions.SubscribingQueueName));
        subscriptionName = destinationManager.StripHierarchyNamespace(subscriptionName);
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
        var topicFilterModes = new Dictionary<string, SubscriptionFilterMode>(StringComparer.OrdinalIgnoreCase);

        foreach (var eventType in eventTypes)
        {
            var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            if (!topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var entries))
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var topicName = destinationManager.GetDestination(entry.Topic, eventTypeFullName);
                if (topicFilterModes.TryGetValue(topicName, out var existingMode))
                {
                    if (existingMode != entry.FilterMode)
                    {
                        return $"Incompatible subscription filter modes detected for topic '{topicName}' on subscription '{subscriptionName}'. Event '{eventTypeFullName}' uses '{entry.FilterMode}' but other events use '{existingMode}'. All events subscribed to the same topic must use the same filter mode on the same endpoint subscription.";
                    }
                }
                else
                {
                    topicFilterModes[topicName] = entry.FilterMode;
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
                .Select(entry => new { Topic = destinationManager.GetDestination(entry.Topic, eventTypeFullName).ToLower(), entry.FilterMode, MessageType = eventTypeFullName }))
            .GroupBy(topicAndMessageType => (topicAndMessageType.Topic, topicAndMessageType.FilterMode))
            .Select(group => new
            {
                TopicName = group.Key.Topic,
                FilterMode = group.Key.FilterMode.ToString(),
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
        var entries = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        return [.. entries.Select(entry => entry with { Topic = destinationManager.GetDestination(entry.Topic, eventTypeFullName) })];
    }

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

        switch (entry.FilterMode)
        {
            case SubscriptionFilterMode.CatchAll:
                await CreateCatchAllSubscription(topicName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false);
                break;
            case SubscriptionFilterMode.CorrelationFilter:
            case SubscriptionFilterMode.SqlFilter:
                await CreateFilteredSubscription(topicName, subscriptionName, eventTypeFullName, entry.FilterMode, creationOptions, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry.FilterMode), entry.FilterMode, "Unknown filter mode");
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
        SubscriptionFilterMode filterMode,
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
            RuleFilter ruleFilter = filterMode == SubscriptionFilterMode.CorrelationFilter
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
        return entry.FilterMode switch
        {
            SubscriptionFilterMode.CatchAll => Task.CompletedTask,
            SubscriptionFilterMode.CorrelationFilter or SubscriptionFilterMode.SqlFilter =>
                DeleteRuleForFilteredSubscription(entry.Topic, eventTypeFullName, subscriptionName, creationOptions, cancellationToken),
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
