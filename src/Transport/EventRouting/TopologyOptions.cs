#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// TODO we probably need some kind of validation method that checks against invalid configurations?
/// </summary>
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

    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, (string TopicToPublishTo, string TopicToSubscribeOn)> EventsToTopicMigrationMap { get; } = [];

    internal string GetPublishDestination(Type eventType)
    {
        var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return publishedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
            @this.EventsToTopicMigrationMap.TryGetValue(fullName,
                out var extractDestination)
                ? extractDestination.TopicToPublishTo
                : @this.PublishedEventToTopicsMap.GetValueOrDefault(fullName, fullName), this);
    }

    internal (string Topic, bool RequiresRule)[] GetSubscribeDestinations(Type eventType)
    {
        var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return subscribedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
            @this.EventsToTopicMigrationMap.TryGetValue(fullName,
                out var extractDestination)
                ? [(extractDestination.TopicToSubscribeOn, true)]
                : @this.SubscribedEventToTopicsMap.GetValueOrDefault(fullName, [fullName]).Select(x => (x, false)).ToArray(), this);
    }

    readonly ConcurrentDictionary<string, string> publishedEventToTopicsCache = new();
    readonly ConcurrentDictionary<string, (string Topic, bool RequiresRule)[]> subscribedEventToTopicsCache = new();
}