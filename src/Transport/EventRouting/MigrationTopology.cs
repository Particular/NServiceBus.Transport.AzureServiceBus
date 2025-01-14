#nullable enable
namespace NServiceBus;

using System;
using System.Linq;
using Transport.AzureServiceBus;

/// <summary>
/// 
/// </summary>
public class MigrationTopology : TopicTopology
{
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public string TopicToPublishTo { get; }

    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    public string TopicToSubscribeOn { get; }

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

    internal MigrationTopology(string topicToPublishTo, string topicToSubscribeOn)
    {
        Guard.AgainstNullAndEmpty(nameof(topicToPublishTo), topicToPublishTo);
        Guard.AgainstNullAndEmpty(nameof(topicToSubscribeOn), topicToSubscribeOn);

        TopicToPublishTo = topicToPublishTo;
        TopicToSubscribeOn = topicToSubscribeOn;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    public MigrationTopology(TopologyOptions options) : base(options)
    {
        //TODO: This obviously would not work so we need to rethink the shape of topology options
        TopicToPublishTo = options.EventsToTopicMigrationMap.Values.First().TopicToPublishTo;
        TopicToSubscribeOn = options.EventsToTopicMigrationMap.Values.First().TopicToSubscribeOn;
    }

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
        _ = Options.EventsToTopicMigrationMap.TryAdd(typeof(TEventType).FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void PublishToDefaultTopic(Type type)
    {
        _ = Options.EventsToTopicMigrationMap.TryAdd(type.FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeToDefaultTopic<TEventType>()
    {
        _ = Options.EventsToTopicMigrationMap.TryAdd(typeof(TEventType).FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SubscribeToDefaultTopic(Type type)
    {
        _ = Options.EventsToTopicMigrationMap.TryAdd(type.FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
    }
}