#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// </summary>
/// TODO Should we rename this to DefaultTopicTopology?
public sealed class TopicPerEventTopology : TopicTopology
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

    /// <inheritdoc />
    protected override string GetPublishDestinationCore(string eventTypeFullName)
        => Options.PublishedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, eventTypeFullName);

    // TODO Should we use a public type instead of this tuple?
    /// <inheritdoc />
    protected override (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)[] GetSubscribeDestinationsCore(string eventTypeFullName, string subscribingQueueName) =>
        // Not caching this Subscribe and Unsubscribe is not really on the hot path.
        Options.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName])
            .Select<string, (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)>(topic => (topic, Options.QueueNameToSubscriptionNameMap.GetValueOrDefault(subscribingQueueName, subscribingQueueName), null))
            .ToArray();
}