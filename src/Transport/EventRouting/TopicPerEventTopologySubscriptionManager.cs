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
    readonly ISubscriptionForwarders subscriptionForwarders;
    volatile bool subscribeAllCalled;

    public TopicPerEventTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions,
        TopologyOptions topologyOptions,
        StartupDiagnosticEntries startupDiagnostic) : base(creationOptions)
    {
        this.topologyOptions = topologyOptions;
        this.startupDiagnostic = startupDiagnostic;
        // The subscription name is limited to 50 characters and the hierarchy is respected by the topic name
        // so there is no need to add it to the subscription name.
        destinationManager = new DestinationManager(topologyOptions.HierarchyOptions);
        var subscribingQueueName = CreationOptions.SubscribingQueueName;
        var strippedSubscribingQueueName = destinationManager.StripHierarchyNamespace(subscribingQueueName);

        var subscriptionNameCandidate =
            topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueName)
            ?? topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(strippedSubscribingQueueName)
            ?? subscribingQueueName;

        subscriptionForwarders = creationOptions.RequiresSession
            ? new SubscriptionForwarders(creationOptions.ForwarderClientFactory!, creationOptions.SubscribingQueueName, creationOptions.TimeBeforeTriggeringCircuitBreaker, creationOptions.CriticalErrorAction)
            : new NullSubscriptionForwarders();

        subscriptionName = destinationManager.StripHierarchyNamespace(subscriptionNameCandidate);
    }

    static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTopologySubscriptionManager>();

    // The event types arriving through the fallback topic are unknown, so this registration is never unsubscribed
    // and keeps the fallback forwarder running until shutdown. The $ prefix cannot clash with a type full name.
    const string FallbackTopicForwardingRegistration = "$fallback-topic";

    // Core calls SubscribeAll before StartReceive unless auto-subscribe is disabled or the endpoint handles no events.
    // Without it, the static topology mapping is the only source of the subscriptions to forward from.
    public override async Task StartForwarders(CancellationToken cancellationToken = default)
    {
        if (CreationOptions.RequiresSession && !subscribeAllCalled)
        {
            await RegisterForwardersFromTopologyMapping(cancellationToken).ConfigureAwait(false);
        }

        await subscriptionForwarders.Start(cancellationToken).ConfigureAwait(false);
    }

    async Task RegisterForwardersFromTopologyMapping(CancellationToken cancellationToken)
    {
        var registrations = (from eventTypeFullName in topologyOptions.SubscribedEventToTopicsMap.Keys
                             from entry in MapEventToSubscriptionEntries(eventTypeFullName)
                             select (entry.Topic, Registration: eventTypeFullName)).ToList();

        if (topologyOptions.FallbackTopic?.TopicName is { Length: > 0 } fallbackTopicName)
        {
            registrations.Add((destinationManager.GetDestination(fallbackTopicName), FallbackTopicForwardingRegistration));
        }

        if (registrations.Count == 0)
        {
            Logger.InfoFormat("No events were subscribed at startup, because auto-subscribe is disabled or the endpoint handles no events, and neither subscribed event mappings nor a fallback topic are configured. Events relying on the default topic convention will only be forwarded from subscription '{0}' to '{1}' after explicit Subscribe calls.", subscriptionName, CreationOptions.SubscribingQueueName);
            return;
        }

        foreach (var (topic, registration) in registrations)
        {
            await subscriptionForwarders.Register(topic, subscriptionName, registration, cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        // Set before provisioning so that a failed auto-subscribe still counts as subscribed
        subscribeAllCalled = true;

        var invalidConfig = ValidateSubscriptionConfiguration(eventTypes);
        if (invalidConfig != null)
        {
            throw new InvalidOperationException(invalidConfig);
        }

        if (eventTypes.Length == 0)
        {
            return;
        }

        WriteSubscriptionManifest(eventTypes);

        var allEntriesByTopic = (from eventType in eventTypes
                                 let eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null")
                                 from entry in MapEventToSubscriptionEntries(eventTypeFullName)
                                 group (eventTypeFullName, entry) by entry.Topic into g
                                 select g).ToArray();

        try
        {
            await Task.WhenAll([.. allEntriesByTopic.Select(group =>
                ProvisionSubscriptionForTopic(group.Key, [.. group], subscriptionName, CreationOptions, cancellationToken))])
                .ConfigureAwait(false);
        }
        finally
        {
            // Also after a failure, so that the subscriptions that already exist are forwarded from
            foreach (var group in allEntriesByTopic)
            {
                foreach ((string eventTypeFullName, SubscriptionEntry entry) entry in group)
                {
                    await subscriptionForwarders.Register(group.Key, subscriptionName, entry.eventTypeFullName, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    static async Task ProvisionSubscriptionForTopic(string topicName,
        (string EventTypeFullName, SubscriptionEntry Entry)[] entries,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        if (creationOptions.SetupInfrastructure)
        {
            await CreateTopicIfNeeded(topicName, creationOptions, cancellationToken).ConfigureAwait(false);
        }

        var hasCatchAll = entries.Any(e => e.Entry.RoutingMode == TopicRoutingMode.NotMultiplexed);
        var filteredEntries = entries.Where(e => e.Entry.RoutingMode is TopicRoutingMode.CorrelationFilter or TopicRoutingMode.SqlLikeFilter).ToArray();

        if (hasCatchAll || filteredEntries.Length == 0)
        {
            await CreateCatchAllSubscription(topicName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false);
            await AddFilterRules(topicName, subscriptionName, filteredEntries, creationOptions, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Rules first, because creating a subscription requires Manage rights even when it exists while managing rules doesn't.
        try
        {
            await AddFilterRules(topicName, subscriptionName, filteredEntries, creationOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            await CreateSubscriptionWithFalseDefaultRule(topicName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false);
            await AddFilterRules(topicName, subscriptionName, filteredEntries, creationOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    static Task AddFilterRules(string topicName, string subscriptionName,
        (string EventTypeFullName, SubscriptionEntry Entry)[] filteredEntries,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken) =>
        Task.WhenAll([.. filteredEntries.Select(e =>
            AddFilterRule(topicName, subscriptionName, e.EventTypeFullName, e.Entry.RoutingMode!.Value, creationOptions, cancellationToken))]);

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
                var effectiveRoutingMode = NormalizeSubscriptionRoutingMode(entry.RoutingMode!.Value);
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
                RoutingMode = $"{group.Key.RoutingMode}",
                MessageTypes = group.Select(x => x.MessageType).ToArray()
            })
            .ToArray();
        startupDiagnostic.Add("Manifest-Subscriptions", subscriptions);
    }

    HashSet<SubscriptionEntry> MapEventToSubscriptionEntries(string eventTypeFullName)
    {
        var entries = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, GetFallbackOrDefaultEntries(eventTypeFullName));
        return [.. entries.Select(entry => entry with { Topic = destinationManager.GetDestination(entry.Topic, eventTypeFullName), RoutingMode = ResolveTopicRoutingMode(entry) })];
    }

    HashSet<SubscriptionEntry> GetFallbackOrDefaultEntries(string eventTypeFullName)
    {
        if (topologyOptions.FallbackTopic?.TopicName is { Length: > 0 } fallbackTopicName)
        {
            return [new SubscriptionEntry(fallbackTopicName, topologyOptions.FallbackTopic.RoutingMode)];
        }

        return [new SubscriptionEntry(eventTypeFullName)];
    }

    TopicRoutingMode ResolveTopicRoutingMode(SubscriptionEntry entry)
    {
        if (entry.RoutingMode.HasValue)
        {
            return entry.RoutingMode.Value;
        }

        // Mapped entries whose topic equals the fallback topic name inherit the fallback mode
        // so that publishing and subscribing stay symmetric with the resolved publish-side mode.
        // The comparison runs against the raw (pre-hierarchy-prefix) entry topic, matching the
        // publish-side resolution which also compares against the raw destination name.
        if (topologyOptions.FallbackTopic is { RoutingMode: not null, TopicName: { Length: > 0 } fallbackTopicName }
            && string.Equals(entry.Topic, fallbackTopicName, StringComparison.Ordinal))
        {
            return topologyOptions.FallbackTopic.RoutingMode.Value;
        }

        return TopicRoutingMode.NotMultiplexed;
    }

    static TopicRoutingMode NormalizeSubscriptionRoutingMode(TopicRoutingMode routingMode) =>
        routingMode switch
        {
            // A subscription without filter rules already receives every message on the topic.
            // Combining it with a correlation-filtered rule is redundant, but Azure Service Bus
            // evaluates the rules using OR semantics, so the combination is harmless and should
            // not be rejected during startup validation.
            TopicRoutingMode.NotMultiplexed or TopicRoutingMode.CorrelationFilter => TopicRoutingMode.NotMultiplexed,
            TopicRoutingMode.SqlLikeFilter => TopicRoutingMode.SqlLikeFilter,
            _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null)
        };

    public override async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        var entries = MapEventToSubscriptionEntries(eventTypeFullName);

        foreach (var entry in entries)
        {
            await subscriptionForwarders.Unregister(entry.Topic, subscriptionName, eventTypeFullName, cancellationToken).ConfigureAwait(false);
        }

        await DeleteSubscriptionsOrRulesForEntries(entries, eventTypeFullName, subscriptionName, CreationOptions, cancellationToken).ConfigureAwait(false);
    }

    public override Task Shutdown(CancellationToken cancellationToken = default) => subscriptionForwarders.Shutdown(cancellationToken);

    static async Task CreateCatchAllSubscription(string topicName, string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            LockDuration = TimeSpan.FromMinutes(5),
            EnableDeadLetteringOnFilterEvaluationExceptions = false,
            MaxDeliveryCount = creationOptions.MaxDeliveryCount,
            EnableBatchedOperations = true,
            UserMetadata = creationOptions.SubscribingQueueName,
            RequiresSession = creationOptions.RequiresSession
        };

        if (!creationOptions.RequiresSession)
        {
            subscriptionOptions.ForwardTo = creationOptions.SubscribingQueueName;
        }

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
            if (await SubscriptionExists(topicName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false))
            {
                Logger.InfoFormat("Subscription {0} could not be created, but it already exists. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                return;
            }

            Logger.ErrorFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
            throw;
        }
    }

    static async Task CreateTopicIfNeeded(string topicName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var topicOptions = new CreateTopicOptions(topicName)
        {
            EnableBatchedOperations = true,
            EnablePartitioning = creationOptions.EnablePartitioning,
            SupportOrdering = creationOptions.RequiresSession,
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

    static async Task CreateSubscriptionWithFalseDefaultRule(string topicName, string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
        {
            LockDuration = TimeSpan.FromMinutes(5),
            EnableDeadLetteringOnFilterEvaluationExceptions = false,
            MaxDeliveryCount = creationOptions.MaxDeliveryCount,
            EnableBatchedOperations = true,
            UserMetadata = creationOptions.SubscribingQueueName,
            RequiresSession = creationOptions.RequiresSession
        };

        if (!creationOptions.RequiresSession)
        {
            subscriptionOptions.ForwardTo = creationOptions.SubscribingQueueName;
        }

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
    }

    // The administration client (SubscriptionExistsAsync) needs Manage rights, which is what we lack here. The rule manager only needs Listen.
    static async Task<bool> SubscriptionExists(string topicName, string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
        var ruleManager = creationOptions.Client.CreateRuleManager(topicName, subscriptionName);
        await using (ruleManager.ConfigureAwait(false))
        {
            try
            {
                await foreach (var _ in ruleManager.GetRulesAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                return true;
            }
            // A missing subscription currently surfaces as a timeout, see Azure/azure-sdk-for-net#64167. A fixed SDK would report it as not found.
            catch (ServiceBusException)
            {
                return false;
            }
            // The SDK maps the service's "not allowed" (browsing a forwarding subscription) to this, which only happens for existing entities.
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    static async Task AddFilterRule(string topicName, string subscriptionName, string eventTypeFullName,
        TopicRoutingMode routingMode,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken)
    {
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

    static Task DeleteSubscriptionsOrRulesForEntries(HashSet<SubscriptionEntry> entries, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken) =>
        Task.WhenAll([
            .. entries.Select(async entry =>
            {
                await DeleteSubscriptionOrRuleForEntry(entry, eventTypeFullName, subscriptionName, creationOptions, cancellationToken).ConfigureAwait(false);
            })
        ]);

    static Task DeleteSubscriptionOrRuleForEntry(SubscriptionEntry entry, string eventTypeFullName,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken) =>
        entry.RoutingMode switch
        {
            TopicRoutingMode.NotMultiplexed => DeleteSubscription(entry.Topic, subscriptionName, creationOptions.AdministrationClient, cancellationToken),
            TopicRoutingMode.CorrelationFilter or TopicRoutingMode.SqlLikeFilter =>
                DeleteRuleForFilteredSubscription(entry.Topic, eventTypeFullName, subscriptionName, creationOptions, cancellationToken),
            _ => DeleteSubscription(entry.Topic, subscriptionName, creationOptions.AdministrationClient, cancellationToken)
        };

    static async Task DeleteSubscription(string topicName, string subscriptionName,
        ServiceBusAdministrationClient administrationClient,
        CancellationToken cancellationToken)
    {
        try
        {
            await administrationClient.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }
        catch (UnauthorizedAccessException unauthorizedAccessException)
        {
            Logger.InfoFormat("Subscription {0} could not be deleted. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
        }
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
        CancellationToken cancellationToken = default) =>
        Task.WhenAll([.. topics.Select(topicName => CreateCatchAllSubscription(topicName, subscriptionName, creationOptions, cancellationToken))]);

    public static Task DeleteSubscriptionsForTopics(HashSet<string> topics, string subscriptionName,
        ServiceBusAdministrationClient administrationClient,
        CancellationToken cancellationToken = default) =>
        Task.WhenAll([.. topics.Select(topicName => DeleteSubscription(topicName, subscriptionName, administrationClient, cancellationToken))]);
}