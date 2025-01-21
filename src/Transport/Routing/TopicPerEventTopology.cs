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
    public string? SubscriptionName
    {
        get => Options.SubscriptionName;
        set => Options.SubscriptionName = value;
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
        Options.PublishedEventToTopicsMap[typeof(TEventType).FullName ?? throw new InvalidOperationException()] =
            topicName;
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
}