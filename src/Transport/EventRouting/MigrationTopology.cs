#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Topology that allows mixing of single-topic and topic-per-event approaches in order to allow gradual migration to the topic-per-event topology.
/// </summary>
public sealed class MigrationTopology : TopicTopology
{
    internal MigrationTopology(MigrationTopologyOptions options) : base(options) => Options = options;

    /// <summary>
    /// Gets the topic name of the topic where all single-topic events are published to.
    /// </summary>
    public string TopicToPublishTo => Options.TopicToPublishTo!;

    /// <summary>
    /// Gets the topic name of the topic where all single-topic subscriptions are managed on.
    /// </summary>
    public string TopicToSubscribeOn => Options.TopicToSubscribeOn!;

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

    new MigrationTopologyOptions Options { get; }

    /// <summary>
    /// Marks the given published event type as migrated applying the default convention of publishing the event type
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked as migrated.</typeparam>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedPublishedEvent<TEventType>() => MigratedPublishedEvent<TEventType>(typeof(TEventType).FullName!);

    /// <summary>
    /// Marks the given published event type as migrated applying the default convention of publishing the event type
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <param name="eventType">The event type to be marked as migrated.</param>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedPublishedEvent(Type eventType) => MigratedPublishedEvent(eventType, eventType.FullName!);

    /// <summary>
    /// Marks the given published event type as migrated and specifies the topic name under which the event is to be published.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked as migrated.</typeparam>
    /// <param name="topicName">The topic name to publish the event to.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedPublishedEvent<TEventType>(string topicName) => MigratedPublishedEvent(typeof(TEventType), topicName);

    /// <summary>
    /// Marks the given published event type as migrated and specifies the topic name under which the event is to be published.
    /// </summary>
    /// <param name="eventType">The event type to be marked as migrated.</param>
    /// <param name="topicName">The topic name to publish the event to.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <exception cref="ArgumentException">The full name of the event is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedPublishedEvent(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        //TODO: Last one wins? Sz: Yes
        Options.PublishedEventToTopicsMap[eventType.FullName] = topicName;
    }

    /// <summary>
    /// Marks the given subscribed event type as migrated applying the default convention of subscribing to the event
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked as migrated.</typeparam>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedSubscribedEvent<TEventType>() => MigratedSubscribedEvent<TEventType>(typeof(TEventType).FullName!);

    /// <summary>
    /// Marks the given subscribed event type as migrated applying the default convention of subscribing to the event
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <param name="eventType">The event type to be marked as migrated.</param>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedSubscribedEvent(Type eventType) => MigratedSubscribedEvent(eventType, eventType.FullName!);

    /// <summary>
    /// Marks the given subscribed event type as migrated and specifies the topic name under which the event is to be subscribed.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked as migrated.</typeparam>
    /// <param name="topicName">The topic name to subscribe to.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedSubscribedEvent<TEventType>(string topicName) => MigratedSubscribedEvent(typeof(TEventType), topicName);

    /// <summary>
    /// Marks the given subscribed event type as migrated and specifies the topic name under which the event is to be subscribed.
    /// </summary>
    /// <param name="eventType">The event type to be marked as migrated.</param>
    /// <param name="topicName">The topic name to subscribe to.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <exception cref="ArgumentException">The full name of the event is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void MigratedSubscribedEvent(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        var eventTypeFullName = eventType.FullName;
        if (Options.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
        {
            topics.Add(topicName);
        }
        else
        {
            Options.SubscribedEventToTopicsMap[eventTypeFullName] = [topicName];
        }
    }

    /// <summary>
    /// Marks the given event type as migrated applying the default convention of subscribing and publishing the event
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked as migrated.</typeparam>
    public void MigratedEvent<TEventType>() => MigratedEvent(typeof(TEventType));

    /// <summary>
    /// Marks the given event type as migrated applying the default convention of subscribing and publishing the event
    /// under a topic name that is the full name of the event type.
    /// </summary>
    /// <param name="eventType">The event type to be marked as migrated.</param>
    public void MigratedEvent(Type eventType)
    {
        MigratedPublishedEvent(eventType);
        MigratedSubscribedEvent(eventType);
    }

    /// <summary>
    /// Marks the given event type to be migrated making sure it is published to the <see cref="TopicToPublishTo"/>
    /// or subscribed to on the <see cref="TopicToSubscribeOn"/> as configured on this migration topology.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked for migration.</typeparam>
    /// <param name="configure">Configures additional event migration options.</param>
    /// <exception cref="ArgumentException">The full name of the event type is not set.</exception>
    /// <remarks>Adding the same event type multiple times automatically de-duplicates the event type.</remarks>
    public void EventToMigrate<TEventType>(Action<EventToMigrateOptions>? configure = null) => EventToMigrate(typeof(TEventType), configure);

    /// <summary>
    /// Marks the given event type to be migrated making sure it is published to the <see cref="TopicToPublishTo"/>
    /// or subscribed to on the <see cref="TopicToSubscribeOn"/> as configured on this migration topology.
    /// </summary>
    /// <param name="eventType">The event type to be marked for migration.</param>
    /// <param name="configure">Configures additional event migration options.</param>
    /// <exception cref="ArgumentException">The full name of the event type is not set.</exception>
    /// <remarks>Adding the same event type multiple times automatically de-duplicates the event type.</remarks>
    public void EventToMigrate(Type eventType, Action<EventToMigrateOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        _ = Options.EventsToMigrateMap.Add(eventType.FullName);

        configure?.Invoke(new EventToMigrateOptions(eventType.FullName, Options));
    }

    /// <summary>
    /// Instructs the topology to use provided subscription name when subscribing to events that are to be forwarded to the given queue.
    /// </summary>
    /// <param name="queueName">Queue name for which the default subscription name is to be overridden.</param>
    /// <param name="subscriptionName">The subscription name to use.</param>
    public void OverrideSubscriptionNameFor(string queueName, string subscriptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        Options.QueueNameToSubscriptionNameMap[queueName] = subscriptionName;
    }

    /// <inheritdoc />
    protected override string GetPublishDestinationCore(string eventTypeFullName)
    {
        if (Options.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
        {
            return TopicToPublishTo;
        }

        if (Options.PublishedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topic))
        {
            return topic;
        }

        // TODO: Much better exception message
        throw new Exception($"During migration every event type must be explicitly mapped. Consider explicitly mapping '{eventTypeFullName}' to a topic.");
    }

    /// <inheritdoc />
    protected override (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)[] GetSubscribeDestinationsCore(string eventTypeFullName, string subscribingQueueName)
    {
        var subscriptionName = Options.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueName, subscribingQueueName);
        if (Options.EventsToMigrateMap.TryGetValue(eventTypeFullName, out _))
        {
            return [(TopicToSubscribeOn, subscriptionName, (Options.SubscribedEventToRuleNameMap.GetValueOrDefault(eventTypeFullName, eventTypeFullName), $"[{Headers.EnclosedMessageTypes}] LIKE '%{eventTypeFullName}%'"))];
        }

        if (Options.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
        {
            // Not caching this Subscribe and Unsubscribe is not really on the hot path.
            return topics.Select<string, (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)>(topic => (topic, subscriptionName, null)).ToArray();
        }

        // TODO: Much better exception message
        throw new Exception($"During migration every event type must be explicitly mapped. Consider explicitly mapping '{eventTypeFullName}' to a topic.");
    }

    /// <summary>
    /// Options enabled additional migration options for a specific event type.
    /// </summary>
    public sealed class EventToMigrateOptions
    {
        internal EventToMigrateOptions(string eventTypeFullName, MigrationTopologyOptions options)
        {
            EventTypeFullName = eventTypeFullName;
            Options = options;
        }

        /// <summary>
        /// Gets the event type full name.
        /// </summary>
        public string EventTypeFullName { get; }
        MigrationTopologyOptions Options { get; }

        /// <summary>
        /// Overrides the default rule name for the event type.
        /// </summary>
        /// <param name="ruleName">The fixed rule name</param>
        /// <exception cref="ArgumentException">The rule name is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The rule is longer than 50 characters.</exception>
        public void OverrideRuleName(string ruleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ruleName.Length, 50, nameof(ruleName));

            Options.SubscribedEventToRuleNameMap[EventTypeFullName] = ruleName;
        }
    }
}