#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

class EventRoutingCache
{
    public EventRoutingCache(TopologyOptions topologyOptions)
    {
        PublishedEventToTopicsMap = topologyOptions.PublishedEventToTopicsMap;
        SubscribedEventToTopicsMap = topologyOptions.SubscribedEventToTopicsMap;

        var migrationTopologyOptions = topologyOptions as MigrationTopologyOptions;
        EventsToMigrateMap = migrationTopologyOptions?.EventsToMigrateMap ?? [];
        TopicToPublishTo = migrationTopologyOptions?.TopicToPublishTo;
        TopicToSubscribeOn = migrationTopologyOptions?.TopicToSubscribeOn;
    }
    public Dictionary<string, string> PublishedEventToTopicsMap { get; }

    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; }

    public HashSet<string> EventsToMigrateMap { get; }

    public string? TopicToPublishTo { get; }
    public string? TopicToSubscribeOn { get; }

    internal string GetPublishDestination(Type eventType)
    {
        var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return publishedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
            @this.EventsToMigrateMap.Contains(fullName)
                ? @this.TopicToPublishTo!
                : @this.PublishedEventToTopicsMap.GetValueOrDefault(fullName, fullName), this);
    }

    internal (string Topic, bool RequiresRule)[] GetSubscribeDestinations(Type eventType)
    {
        var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return subscribedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
            @this.EventsToMigrateMap.Contains(fullName)
                ? [(@this.TopicToSubscribeOn!, true)]
                : @this.SubscribedEventToTopicsMap.GetValueOrDefault(fullName, [fullName]).Select(x => (x, false)).ToArray(), this);
    }

    readonly ConcurrentDictionary<string, string> publishedEventToTopicsCache = new();
    readonly ConcurrentDictionary<string, (string Topic, bool RequiresRule)[]> subscribedEventToTopicsCache = new();
}

/// <summary>
/// TODO we probably need some kind of validation method that checks against invalid configurations?
/// </summary>
[JsonDerivedType(typeof(MigrationTopologyOptions))]
[JsonDerivedType(typeof(TopicPerEventTopologyOptions))]
public class TopologyOptions
{
    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> PublishedEventToTopicsMap { get; } = [];

    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; } = [];
}

/// <summary>
/// 
/// </summary>
public class TopicPerEventTopologyOptions : TopologyOptions
{
    public string? SubscriptionName { get; init; }
}

/// <summary>
/// 
/// </summary>
public class MigrationTopologyOptions : TopologyOptions
{
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public required string TopicToPublishTo { get; init; }

    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    public required string TopicToSubscribeOn { get; init; }

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    [JsonIgnore]
    public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> EventsToMigrateMap { get; } = [];
}