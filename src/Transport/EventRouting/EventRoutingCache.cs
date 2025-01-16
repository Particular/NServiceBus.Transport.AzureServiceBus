#nullable enable

namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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