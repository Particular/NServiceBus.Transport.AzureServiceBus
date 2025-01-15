#nullable enable
namespace NServiceBus;

using System;

/// <summary>
/// 
/// </summary>
public class MigrationTopology : TopicTopology
{
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public string TopicToPublishTo => Options.TopicToPublishTo;

    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    public string TopicToSubscribeOn => Options.TopicToSubscribeOn;

    internal new MigrationTopologyOptions Options { get; }

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

    internal MigrationTopology(MigrationTopologyOptions options) : base(options) => Options = options;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="topicName"></param>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishTo<TEventType>(string topicName)
    {
        // TODO Last one wins?
        Options.PublishedEventToTopicsMap[typeof(TEventType).FullName ?? throw new InvalidOperationException()] = topicName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <param name="topicName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishTo(Type type, string topicName)
    {
        // TODO Last one wins?
        Options.PublishedEventToTopicsMap[type.FullName ?? throw new InvalidOperationException()] = topicName;
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
    /// <param name="type"></param>
    /// <param name="topicName"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeTo(Type type, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(type.FullName);

        var eventTypeFullName = type.FullName;
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
    public void PublishToDefaultTopic<TEventType>()
    {
        _ = Options.EventsToMigrateMap.Add(typeof(TEventType).FullName ?? throw new InvalidOperationException());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishToDefaultTopic(Type type)
    {
        _ = Options.EventsToMigrateMap.Add(type.FullName ?? throw new InvalidOperationException());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeToDefaultTopic<TEventType>()
    {
        _ = Options.EventsToMigrateMap.Add(typeof(TEventType).FullName ?? throw new InvalidOperationException());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeToDefaultTopic(Type type)
    {
        _ = Options.EventsToMigrateMap.Add(type.FullName ?? throw new InvalidOperationException());
    }
}