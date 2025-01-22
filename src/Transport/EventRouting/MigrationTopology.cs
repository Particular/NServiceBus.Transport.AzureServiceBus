#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// </summary>
public sealed class MigrationTopology : TopicTopology
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
    /// <typeparam name="TEventType"></typeparam>
    public void MigratedPublishedEvent<TEventType>() => MigratedPublishedEvent<TEventType>(typeof(TEventType).FullName!);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    public void MigratedPublishedEvent(Type eventType) => MigratedPublishedEvent(eventType, eventType.FullName!);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="topicName"></param>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void MigratedPublishedEvent<TEventType>(string topicName) => MigratedPublishedEvent(typeof(TEventType), topicName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="topicName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void MigratedPublishedEvent(Type eventType, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType.FullName);

        // TODO Last one wins?
        Options.PublishedEventToTopicsMap[eventType.FullName] = topicName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    public void MigratedSubscribedEvent<TEventType>() => MigratedSubscribedEvent<TEventType>(typeof(TEventType).FullName!);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    public void MigratedSubscribedEvent(Type eventType) => MigratedSubscribedEvent(eventType, eventType.FullName!);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="topicName"></param>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void MigratedSubscribedEvent<TEventType>(string topicName) => MigratedSubscribedEvent(typeof(TEventType), topicName);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="topicName"></param>
    /// <exception cref="InvalidOperationException"></exception>
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
    /// 
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    public void MigratedEvent<TEventType>() => MigratedEvent(typeof(TEventType));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void MigratedEvent(Type eventType)
    {
        MigratedPublishedEvent(eventType);
        MigratedSubscribedEvent(eventType);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void EventToMigrate<TEventType>() => EventToMigrate(typeof(TEventType));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="eventType"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void EventToMigrate(Type eventType)
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
}