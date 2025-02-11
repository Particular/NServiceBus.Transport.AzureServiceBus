namespace NServiceBus.Transport.AzureServiceBus;

using System;

/// <summary>
/// Topology that allows mixing of single-topic and topic-per-event approaches in order to allow gradual migration to the topic-per-event topology.
/// </summary>
public sealed class MigrationTopology : TopicTopology
{
    internal MigrationTopology(MigrationTopologyOptions options) : base(options) => Options = options;

    /// <summary>
    /// Gets the topic name of the topic where all single-topic events are published to.
    /// </summary>
    public new string TopicToPublishTo => Options.TopicToPublishTo!;

    /// <summary>
    /// Gets the topic name of the topic where all single-topic events are subscribed.
    /// </summary>
    public new string TopicToSubscribeOn => Options.TopicToSubscribeOn!;

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy (different publish and subscribe topics).
    /// </summary>
    public new bool IsHierarchy =>
        !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

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
    /// Marks the given event type to be migrated making sure it is published to the <see cref="TopicToPublishTo"/>
    /// or subscribed to on the <see cref="TopicToSubscribeOn"/> as configured on this migration topology.
    /// </summary>
    /// <typeparam name="TEventType">The event type to be marked for migration.</typeparam>
    /// <param name="ruleNameOverride">Optional rule name override.</param>
    /// <exception cref="ArgumentException">The full name of the event type is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning (setting the <paramref name="ruleNameOverride"/> value).</remarks>
    public void EventToMigrate<TEventType>(string? ruleNameOverride = null) => EventToMigrate(typeof(TEventType), ruleNameOverride);

    /// <summary>
    /// Marks the given event type to be migrated making sure it is published to the <see cref="TopicToPublishTo"/>
    /// or subscribed to on the <see cref="TopicToSubscribeOn"/> as configured on this migration topology.
    /// </summary>
    /// <param name="eventType">The event type to be marked for migration.</param>
    /// <param name="ruleNameOverride">Optional rule name override.</param>
    /// <exception cref="ArgumentException">The full name of the event type is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning (setting the <paramref name="ruleNameOverride"/> value).</remarks>
    public void EventToMigrate(Type eventType, string? ruleNameOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        _ = Options.EventsToMigrateMap.Add(eventType.FullName);

        if (ruleNameOverride is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ruleNameOverride);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ruleNameOverride.Length, 50, nameof(ruleNameOverride));
        Options.SubscribedEventToRuleNameMap[eventType.FullName] = ruleNameOverride;
    }

    /// <summary>
    /// Instructs the topology to use provided subscription name when subscribing to events that are to be forwarded to the given queue.
    /// </summary>
    /// <param name="queueName">Queue name for which the default subscription name is to be overridden.</param>
    /// <param name="subscriptionName">The subscription name to use.</param>
    /// <remarks>Calling this method multiple times with the same queue name will lead to the last one winning (setting the <paramref name="subscriptionName"/> value).</remarks>
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

        throw new Exception($"When using migration topology, every event needs to be marked either as migrated or pending migration to avoid message loss. In the topology configuration use either MigratedPublishedEvent<'{eventTypeFullName}'>() or EventToMigrate<'{eventTypeFullName}'>(), depending on the migration state of this event.");
    }

    internal override SubscriptionManager CreateSubscriptionManager(SubscriptionManagerCreationOptions creationOptions) => new MigrationTopologySubscriptionManager(creationOptions, Options);
}