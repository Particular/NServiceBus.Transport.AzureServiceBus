namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Serializable object that defines the topic-per-event topology
/// </summary>
[JsonDerivedType(typeof(TopologyOptions), typeDiscriminator: "topology-options")]
[JsonDerivedType(typeof(MigrationTopologyOptions), typeDiscriminator: "migration-topology-options")]
public class TopologyOptions
{
    /// <summary>
    /// Maps event type full names to topics under which they are to be published.
    /// </summary>
    [AzureServiceBusTopics]
    public Dictionary<string, string> PublishedEventToTopicsMap
    {
        get => publishedEventToTopicsMap;
        init => publishedEventToTopicsMap = value ?? [];
    }

    /// <summary>
    /// Maps event type full names to topics under which they are to be subscribed.
    /// </summary>
    [AzureServiceBusTopics]
    [JsonConverter(typeof(SubscribedEventToTopicsMapConverter))]
    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap
    {
        get => subscribedEventToTopicsMap;
        init => subscribedEventToTopicsMap = value ?? [];
    }

    /// <summary>
    /// Maps queue names to non-default subscription names.
    /// </summary>
    [AzureServiceBusQueues]
    [AzureServiceBusSubscriptions]
    public Dictionary<string, string> QueueNameToSubscriptionNameMap
    {
        get => queueNameToSubscriptionNameMap;
        init => queueNameToSubscriptionNameMap = value ?? [];
    }

    //Backing fields are required because the Json serializes initializes properties to null if corresponding json element is missing
    readonly Dictionary<string, string> publishedEventToTopicsMap = [];
    readonly Dictionary<string, HashSet<string>> subscribedEventToTopicsMap = [];
    readonly Dictionary<string, string> queueNameToSubscriptionNameMap = [];

    /// <summary>
    /// Determines if an exception should be thrown when attempting to publish an event not mapped in PublishedEventToTopicsMap
    /// </summary>
    public bool ThrowIfUnmappedEventTypes { get; set; } = false;
}