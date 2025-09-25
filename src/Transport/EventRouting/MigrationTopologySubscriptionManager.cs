namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
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

#pragma warning disable CS0618 // Type or member is obsolete
    public MigrationTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions, MigrationTopologyOptions topologyOptions, StartupDiagnosticEntries startupDiagnostic) : base(creationOptions)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        this.topologyOptions = topologyOptions;
        this.startupDiagnostic = startupDiagnostic;
        subscriptionName = topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(CreationOptions.SubscribingQueueName, CreationOptions.SubscribingQueueName);
    }

    static readonly ILog Logger = LogManager.GetLogger<MigrationTopologySubscriptionManager>();

    public override Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        //NOTE: identical to code in EventPerTopicTopolocySubscriptionManager but kept separate due to this class being obsolete
        var subscriptions = eventTypes
            .Select(eventType => eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"))
            .SelectMany(eventTypeFullName =>
                topologyOptions.SubscribedEventToTopicsMap
                .GetValueOrDefault(eventTypeFullName, [eventTypeFullName])
                .Select(topicName => new { Topic = topicName.ToLower(), MessageType = eventTypeFullName }))
            .GroupBy(topicAndMessageType => topicAndMessageType.Topic)
            .Select(group => new
            {
                TopicName = group.Key,
                MessageTypes = group.Select(topicAndMessageType => topicAndMessageType.MessageType).ToArray()
            })
            .ToArray();
        startupDiagnostic.Add("Manifest-Subscriptions", subscriptions);

        return eventTypes.Length switch
        {
            0 => Task.CompletedTask,
            1 => SubscribeEvent(eventTypes[0].MessageType.FullName!, cancellationToken),
            _ => Task.WhenAll(eventTypes.Select(eventType =>
                    SubscribeEvent(eventType.MessageType.FullName!, cancellationToken))
                .ToArray())
        };
    }

    async Task SubscribeEvent(string eventTypeFullName, CancellationToken cancellationToken)
    {
        if (topologyOptions.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
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
            return;
        }

        if (topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
        {
            await TopicPerEventTopologySubscriptionManager
                .CreateSubscriptionsForTopics(topics, subscriptionName, CreationOptions, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        throw new Exception($"When using migration topology, every event needs to be marked either as migrated or pending migration to avoid message loss. In the topology configuration use either MigratedSubscribedEvent<'{eventTypeFullName}'>() or EventToMigrate<'{eventTypeFullName}'>(), depending on the migration state of this event.");
    }

    public override async Task Unsubscribe(MessageMetadata eventType, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");

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

        if (topologyOptions.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
        {
            await TopicPerEventTopologySubscriptionManager.DeleteSubscriptionsForTopics(topics, subscriptionName,
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
            MaxDeliveryCount = int.MaxValue,
            EnableBatchedOperations = true,
            UserMetadata = CreationOptions.SubscribingQueueName
        };

        try
        {
            await CreationOptions.AdministrationClient.CreateSubscriptionAsync(subscription,
                new CreateRuleOptions("$default", new FalseRuleFilter()), cancellationToken).ConfigureAwait(false);
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
}