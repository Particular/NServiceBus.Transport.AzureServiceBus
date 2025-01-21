#nullable enable
namespace NServiceBus;

using System;

/// <summary>
/// 
/// </summary>
public class TopicPerEventTopology : TopicTopology
{
    internal TopicPerEventTopology(TopicPerEventTopologyOptions options) : base(options) => Options = options;

    new TopicPerEventTopologyOptions Options { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="topicName"></param>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishTo<TEventType>(string topicName) => PublishTo(typeof(TEventType), topicName);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="topicName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishTo(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        // TODO Last one wins?
        Options.PublishedEventToTopicsMap[eventType.FullName] = topicName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="topicName"></param>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeTo<TEventType>(string topicName) => SubscribeTo(typeof(TEventType), topicName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="topicName"></param>
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
    /// 
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="subscriptionName"></param>
    public void OverrideSubscriptionNameFor(string queueName, string subscriptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        Options.QueueNameToSubscriptionNameMap[queueName] = subscriptionName;
    }
}