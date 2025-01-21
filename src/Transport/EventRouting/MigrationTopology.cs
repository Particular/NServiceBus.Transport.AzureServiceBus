#nullable enable
namespace NServiceBus;

using System;

/// <summary>
/// 
/// </summary>
public class MigrationTopology : TopicTopology
{
    internal MigrationTopology(MigrationTopologyOptions options) : base(options) => Options = options;

    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public string TopicToPublishTo => Options.TopicToPublishTo!;

    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    public string TopicToSubscribeOn => Options.TopicToSubscribeOn!;


    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    public bool IsHierarchy => Options.IsHierarchy;

    new MigrationTopologyOptions Options { get; }

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
        Options.PublishedEventToTopicsMap[eventType.FullName ?? throw new InvalidOperationException()] = topicName;
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
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void MapToDefaultTopic<TEventType>() => MapToDefaultTopic(typeof(TEventType));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void MapToDefaultTopic(Type eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        _ = Options.EventsToMigrateMap.Add(eventType.FullName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ruleName"></param>
    /// <typeparam name="TEventType"></typeparam>
    public void OverrideRuleNameFor<TEventType>(string ruleName) => OverrideRuleNameFor(typeof(TEventType), ruleName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="ruleName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void OverrideRuleNameFor(Type eventType, string ruleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        Options.SubscribedEventToRuleNameMap[eventType.FullName] = ruleName;
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