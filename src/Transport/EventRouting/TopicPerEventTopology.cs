#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// A topology that uses separate topic for each event type.
/// </summary>
public sealed class TopicPerEventTopology : TopicTopology
{
    //TODO: Should we rename this to DefaultTopicTopology? Sz: No

    internal TopicPerEventTopology(TopologyOptions options) : base(options)
    {
    }

    /// <summary>
    /// Instructs the topology to use provided topic to publish events of a given type.
    /// </summary>
    /// <param name="topicName">Name of the topic to publish to.</param>
    /// <typeparam name="TEventType">Type of the event.</typeparam>
    public void PublishTo<TEventType>(string topicName) => PublishTo(typeof(TEventType), topicName);

    /// <summary>
    /// Instructs the topology to use provided topic to publish events of a given type.
    /// </summary>
    /// <param name="eventType">Name of the topic to publish to.</param>
    /// <param name="topicName">Type of the event.</param>
    public void PublishTo(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        //TODO: Last one wins? Sz: Yes
        Options.PublishedEventToTopicsMap[eventType.FullName] = topicName;
    }

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type.
    /// </summary>
    /// <param name="topicName">Name of the topic to subscribe to.</param>
    /// <typeparam name="TEventType">Type of the event.</typeparam>
    public void SubscribeTo<TEventType>(string topicName) => SubscribeTo(typeof(TEventType), topicName);

    /// <summary>
    /// Instructs the topology to use provided topic to subscribe for events of a given type.
    /// </summary>
    /// <param name="eventType">Name of the topic to subscribe to.</param>
    /// <param name="topicName">Type of the event.</param>
    /// <exception cref="InvalidOperationException"></exception>
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
        => Options.PublishedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, eventTypeFullName);

    //TODO: Should we use a public type instead of this tuple? Sz: Yes!
    /// <inheritdoc />
    protected override SubscriptionInfo[] GetSubscribeDestinationsCore(string eventTypeFullName, string subscribingQueueName) =>
        // Not caching this Subscribe and Unsubscribe is not really on the hot path.
        Options.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName])
            .Select(topic => new SubscriptionInfo
            {
                Topic = topic,
                SubscriptionName = Options.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueName, subscribingQueueName)
            })
            .ToArray();
}