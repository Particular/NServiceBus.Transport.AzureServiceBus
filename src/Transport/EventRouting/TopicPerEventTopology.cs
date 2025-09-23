namespace NServiceBus.Transport.AzureServiceBus;

using System;

/// <summary>
/// A topology that uses separate topic for each event.
/// </summary>
public sealed class TopicPerEventTopology : TopicTopology
{
    internal TopicPerEventTopology(TopologyOptions options) : base(options, new TopologyOptionsValidator())
    {
    }

    /// <summary>
    /// Instructs the topology to use provided topic to publish events of a given type.
    /// </summary>
    /// <param name="topicName">Name of the topic to publish to.</param>
    /// <typeparam name="TEventType">Type of the event.</typeparam>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void PublishTo<TEventType>(string topicName) => PublishTo(typeof(TEventType), topicName);

    /// <summary>
    /// Instructs the topology to use provided topic to publish events of a given type.
    /// </summary>
    /// <param name="eventType">Name of the topic to publish to.</param>
    /// <param name="topicName">Type of the event.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <exception cref="ArgumentException">The event type is not set.</exception>
    /// <remarks>Calling overloads of this method multiple times with the same event type will lead to the last one winning.</remarks>
    public void PublishTo(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        Options.PublishedEventToTopicsMap[eventType.FullName] = topicName;
    }

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type applying the default convention
    /// of subscribing to the event under a topic name that is the full name of the event type.
    /// </summary>
    /// <typeparam name="TEventType">Type of the event.</typeparam>
    /// <typeparam name="TEventTypeImplementation">Type of the event implementation.</typeparam>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    public void SubscribeTo<TEventType, TEventTypeImplementation>() where TEventTypeImplementation : TEventType =>
        SubscribeTo(typeof(TEventType), typeof(TEventTypeImplementation));

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type applying the default convention
    /// of subscribing to the event under a topic name that is the full name of the event type.
    /// </summary>
    /// <param name="eventType">Name of the topic to subscribe to.</param>
    /// <param name="eventTypeImplementation">Type of the event implementation.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    public void SubscribeTo(Type eventType, Type eventTypeImplementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeImplementation.FullName);
        SubscribeTo(eventType, eventTypeImplementation.FullName);
    }

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type.
    /// </summary>
    /// <param name="topicName">Name of the topic to subscribe to.</param>
    /// <typeparam name="TEventType">Type of the event.</typeparam>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    public void SubscribeTo<TEventType>(string topicName) => SubscribeTo(typeof(TEventType), topicName);

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type.
    /// </summary>
    /// <param name="eventType">Name of the topic to subscribe to.</param>
    /// <param name="topicName">Type of the event.</param>
    /// <exception cref="ArgumentException">The topic name is not set.</exception>
    /// <exception cref="ArgumentException">The event type is not set.</exception>
    public void SubscribeTo(Type eventType, string topicName)
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
        if (!Options.PublishedEventToTopicsMap.TryGetValue(eventTypeFullName, out string? topic) && Options.ThrowIfUnmappedEventTypes)
        {
            throw new Exception($"Unmapped event type '{eventTypeFullName}'. All events must be mapped in `{nameof(TopologyOptions.PublishedEventToTopicsMap)}` when `{nameof(TopologyOptions.ThrowIfUnmappedEventTypes)}` is set");
        }
        return topic ?? eventTypeFullName;
    }

    internal override SubscriptionManager CreateSubscriptionManager(
        SubscriptionManagerCreationOptions creationOptions, HostSettings hostSettings) =>
        new TopicPerEventTopologySubscriptionManager(creationOptions, Options, hostSettings.StartupDiagnostic);
}