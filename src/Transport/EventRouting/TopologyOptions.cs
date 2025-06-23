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
        get;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// Maps event type full names to topics under which they are to be subscribed.
    /// </summary>
    [AzureServiceBusTopics]
    [JsonConverter(typeof(SubscribedEventToTopicsMapConverter))]
    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap
    {
        get;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// Maps queue names to non-default subscription names.
    /// </summary>
    [AzureServiceBusQueues]
    [AzureServiceBusSubscriptions]
    public Dictionary<string, string> QueueNameToSubscriptionNameMap
    {
        get;
        init => field = value ?? [];
    } = [];
}