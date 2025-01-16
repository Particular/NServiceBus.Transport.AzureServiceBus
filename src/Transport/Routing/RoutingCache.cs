#nullable enable

namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

class RoutingCache
{
    public RoutingCache(TopologyOptions topologyOptions)
    {
        OutgoingMessagesToTopicsMap = topologyOptions.PublishedEventToTopicsMap;
        SubscribedEventToTopicsMap = topologyOptions.SubscribedEventToTopicsMap;

        var migrationTopologyOptions = topologyOptions as MigrationTopologyOptions;
        MessagesToMigrateMap = migrationTopologyOptions?.EventsToMigrateMap ?? [];
        TopicToPublishTo = migrationTopologyOptions?.TopicToPublishTo;
        TopicToSubscribeOn = migrationTopologyOptions?.TopicToSubscribeOn;
    }
    public Dictionary<string, string> OutgoingMessagesToTopicsMap { get; }

    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; }

    public HashSet<string> MessagesToMigrateMap { get; }

    public string? TopicToPublishTo { get; }
    public string? TopicToSubscribeOn { get; }

    public string GetDestination(IOutgoingTransportOperation outgoingTransportOperation)
    {
        switch (outgoingTransportOperation)
        {
            case MulticastTransportOperation multicastTransportOperation:
                return GetDestinationOrDefault(multicastTransportOperation.MessageType);
            case UnicastTransportOperation unicastTransportOperation:
                string destination = unicastTransportOperation.Destination;
                if (unicastTransportOperation.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var enclosedMessageTypes) && enclosedMessageTypes != null)
                {
                    var messageTypeSplitterIndex = enclosedMessageTypes.IndexOf(',');

                    destination = GetDestinationOrDefault(
                        messageTypeSplitterIndex > -1
                            ? enclosedMessageTypes[..messageTypeSplitterIndex]
                            : enclosedMessageTypes, unicastTransportOperation.Destination);
                }

                // Workaround for reply-to address set by ASB transport
                var index = unicastTransportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination[..index];
                }

                return destination;
            default:
                throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
        }
    }

    string GetDestinationOrDefault(Type messageType)
    {
        var messageTypeFullName = messageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return GetDestinationOrDefault(messageTypeFullName);
    }

    string GetDestinationOrDefault(string messageTypeFullName, string? defaultDestination = null) =>
        publishedEventToTopicsCache.GetOrAdd(messageTypeFullName, static (fullName, state) =>
        {
            var (@this, defaultDestination) = state;
            return @this.MessagesToMigrateMap.Contains(fullName)
                ? @this.TopicToPublishTo!
                : defaultDestination ?? @this.OutgoingMessagesToTopicsMap.GetValueOrDefault(fullName, fullName);
        }, (this, defaultDestination));

    internal (string Topic, bool RequiresRule)[] GetSubscribeDestinations(Type eventType)
    {
        var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        return subscribedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
            @this.MessagesToMigrateMap.Contains(fullName)
                ? [(@this.TopicToSubscribeOn!, true)]
                : @this.SubscribedEventToTopicsMap.GetValueOrDefault(fullName, [fullName]).Select(x => (x, false)).ToArray(), this);
    }

    readonly ConcurrentDictionary<string, string> publishedEventToTopicsCache = new();
    readonly ConcurrentDictionary<string, (string Topic, bool RequiresRule)[]> subscribedEventToTopicsCache = new();
}