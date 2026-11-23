namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using EventRouting;
using Extensibility;
using Logging;
using Unicast.Messages;

sealed class MigrationTopologySubscriptionManager : SubscriptionManager
{
#pragma warning disable CS0618 // Type or member is obsolete
    readonly MigrationTopologyOptions topologyOptions;
#pragma warning restore CS0618 // Type or member is obsolete
    readonly StartupDiagnosticEntries startupDiagnostic;
    readonly string subscriptionName;
    readonly DestinationManager destinationManager;

#pragma warning disable CS0618 // Type or member is obsolete
    public MigrationTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions, MigrationTopologyOptions topologyOptions, StartupDiagnosticEntries startupDiagnostic) : base(creationOptions)
#pragma warning restore CS0618 // Type or member is obsolete
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

        subscriptionName = destinationManager.StripHierarchyNamespace(subscriptionNameCandidate);
    }

    static readonly ILog Logger = LogManager.GetLogger<MigrationTopologySubscriptionManager>();

    public override Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        WriteSubscriptionManifest(eventTypes);

        if (CreationOptions.AssertTopologyExists)
        {
            return SubscribeThroughExistingSubscriptions(eventTypes, cancellationToken);
        }

        return eventTypes.Length switch
        {
            0 => Task.CompletedTask,
            1 => SubscribeEvent(eventTypes[0].MessageType.FullName!, cancellationToken),
            _ => Task.WhenAll(eventTypes.Select(eventType =>
                    SubscribeEvent(eventType.MessageType.FullName!, cancellationToken))
                .ToArray())
        };
    }

    void WriteSubscriptionManifest(MessageMetadata[] eventTypes)
    {
        //NOTE: identical to code in EventPerTopicTopolocySubscriptionManager but kept separate due to this class being obsolete
        var subscriptions = eventTypes
            .Select(eventType => eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"))
            .SelectMany(eventTypeFullName => MapEventToDestinationTopics(eventTypeFullName)
                    .Select(topicName => new { Topic = topicName.ToLower(), MessageType = eventTypeFullName }))
            .GroupBy(topicAndMessageType => topicAndMessageType.Topic)
            .Select(group => new
            {
                TopicName = group.Key,
                MessageTypes = group.Select(topicAndMessageType => topicAndMessageType.MessageType).ToArray()
            })
            .ToArray();
        startupDiagnostic.Add("Manifest-Subscriptions", subscriptions);
    }

    HashSet<string> MapEventToDestinationTopics(string eventTypeFullName)
    {
        var entries = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        return [.. entries.Select(e => destinationManager.GetDestination(e.Topic, eventTypeFullName))];
    }

    async Task SubscribeEvent(string eventTypeFullName, CancellationToken cancellationToken)
    {
        if (topologyOptions.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
        {
            await CreateMigrationRule(eventTypeFullName, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var entries))
        {
            var topics = entries.Select(e => destinationManager.GetDestination(e.Topic, eventTypeFullName));
            await TopicPerEventTopologySubscriptionManager
                .CreateSubscriptionsForTopics([.. topics], subscriptionName, CreationOptions, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        throw UnmarkedEvent(eventTypeFullName);
    }

    // Events pending migration need a rule on the subscription of the migration topic; migrated events need only their subscriptions
    async Task SubscribeThroughExistingSubscriptions(MessageMetadata[] eventTypes, CancellationToken cancellationToken)
    {
        var eventsByTopic = new Dictionary<string, List<(string EventTypeFullName, bool NeedsRule)>>(StringComparer.Ordinal);
        var unmarkedEvents = new List<string>();

        foreach (var eventType in eventTypes)
        {
            var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");

            if (topologyOptions.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
            {
                EventsOn(topologyOptions.TopicToSubscribeOn ?? throw new InvalidOperationException("The migration topology has no topic to subscribe on")).Add((eventTypeFullName, true));
            }
            else if (topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var entries))
            {
                foreach (var entry in entries)
                {
                    EventsOn(destinationManager.GetDestination(entry.Topic, eventTypeFullName)).Add((eventTypeFullName, false));
                }
            }
            else
            {
                unmarkedEvents.Add(eventTypeFullName);
            }
        }

        var results = await Task.WhenAll(eventsByTopic.Select(async topic =>
            (topic.Key, topic.Value.Select(e => e.EventTypeFullName).Distinct().ToArray(), await AssertSubscription(topic.Key, subscriptionName, async token =>
            {
                foreach (var (eventTypeFullName, _) in topic.Value.Where(e => e.NeedsRule))
                {
                    await CreateMigrationRule(eventTypeFullName, token).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false)))).ConfigureAwait(false);

        // Recorded with the checked topics, so that an unmarked event does not prevent checking the marked ones
        RecordSubscribingResults([.. results, .. unmarkedEvents.Select(e => (string.Empty, new[] { e }, (SubscribingFailure?)new SubscribingFailure(UnmarkedEvent(e).Message)))]);

        List<(string EventTypeFullName, bool NeedsRule)> EventsOn(string topicName)
        {
            if (!eventsByTopic.TryGetValue(topicName, out var events))
            {
                events = [];
                eventsByTopic[topicName] = events;
            }

            return events;
        }
    }

    async Task CreateMigrationRule(string eventTypeFullName, CancellationToken cancellationToken)
    {
        var ruleManager = CreationOptions.Client.CreateRuleManager(topologyOptions.TopicToSubscribeOn, subscriptionName);
        await using (ruleManager.ConfigureAwait(false))
        {
            try
            {
                var ruleName =
                    topologyOptions.SubscribedEventToRuleNameMap.GetValueOrDefault(eventTypeFullName, eventTypeFullName);
                await ruleManager.CreateRuleAsync(new CreateRuleOptions(ruleName, new SqlRuleFilter($"[{Headers.EnclosedMessageTypes}] LIKE '%{eventTypeFullName}%'")), cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
        }
    }

    static Exception UnmarkedEvent(string eventTypeFullName) =>
        new($"When using migration topology, every event needs to be marked either as migrated or pending migration to avoid message loss. In the topology configuration use either MigratedSubscribedEvent<'{eventTypeFullName}'>() or EventToMigrate<'{eventTypeFullName}'>(), depending on the migration state of this event.");

    public override async Task Unsubscribe(MessageMetadata eventType, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        ThrowIfUnsubscribingIsNotSupported(eventTypeFullName);

        if (topologyOptions.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
        {
            var ruleManager = CreationOptions.Client.CreateRuleManager(topologyOptions.TopicToSubscribeOn, subscriptionName);
            await using (ruleManager.ConfigureAwait(false))
            {
                try
                {
                    var ruleName =
                        topologyOptions.SubscribedEventToRuleNameMap.GetValueOrDefault(eventTypeFullName, eventTypeFullName);
                    await ruleManager.DeleteRuleAsync(ruleName, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                }
            }
            return;
        }

        if (topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var entries))
        {
            var topics = entries.Select(e => destinationManager.GetDestination(e.Topic, eventTypeFullName));
            await TopicPerEventTopologySubscriptionManager.DeleteSubscriptionsForTopics([.. topics], subscriptionName,
                    CreationOptions.AdministrationClient, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        throw new Exception($"When using migration topology, every events needs to be marked either as migrated or pending migration to avoid message loss. In the topology configuration use either MigratedSubscribedEvent<'{eventTypeFullName}'>() or EventToMigrate<'{eventTypeFullName}'>(), depending on the migration state of this event.");
    }

    protected override async ValueTask SetupInfrastructureCore(CancellationToken cancellationToken = default)
    {
        var subscription = new CreateSubscriptionOptions(topologyOptions.TopicToSubscribeOn, subscriptionName)
        {
            LockDuration = TimeSpan.FromMinutes(5),
            ForwardTo = CreationOptions.SubscribingQueueName,
            EnableDeadLetteringOnFilterEvaluationExceptions = false,
            MaxDeliveryCount = CreationOptions.MaxDeliveryCount,
            EnableBatchedOperations = true,
            UserMetadata = CreationOptions.SubscribingQueueName
        };

        try
        {
            await SubscriptionCreation.CreateWithRetries(subscription, CreationOptions.SubscriptionCreationRetryOptions,
                token => CreationOptions.AdministrationClient.CreateSubscriptionAsync(subscription,
                    new CreateRuleOptions("$default", new FalseRuleFilter()), token), cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Default subscription rule for topic {subscription.TopicName} already exists");
            }
        }
        catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
        {
            Logger.Info($"Default subscription rule for topic {subscription.TopicName} is already in progress");
        }
    }

    public override Task ShutdownForwarders(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
