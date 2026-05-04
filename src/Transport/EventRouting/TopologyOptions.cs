namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Serializable object that defines the topic-per-event topology
/// </summary>
[JsonDerivedType(typeof(TopologyOptions), typeDiscriminator: "topology-options")]
#pragma warning disable CS0618 // Type or member is obsolete
[JsonDerivedType(typeof(MigrationTopologyOptions), typeDiscriminator: "migration-topology-options")]
#pragma warning restore CS0618 // Type or member is obsolete
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
    public Dictionary<string, HashSet<SubscriptionEntry>> SubscribedEventToTopicsMap
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

    /// <summary>
    /// Maps event type full names to routing options.
    /// </summary>
    public Dictionary<string, RoutingOptions> RoutingOptionsMap
    {
        get;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// Shared fallback topic configuration for unmapped events.
    /// </summary>
    public FallbackTopicOptions? FallbackTopic { get; set; }

    /// <summary>
    /// Determines if an exception should be thrown when attempting to publish an event not mapped in <see cref="PublishedEventToTopicsMap"/>.
    /// If <see cref="FallbackTopic"/> is configured, otherwise-unmapped events are routed to that fallback topic instead and no exception is thrown.
    /// </summary>
    public bool ThrowIfUnmappedEventTypes { get; set; } = false;

    [JsonIgnore]
    internal HierarchyNamespaceOptions HierarchyNamespaceOptions
    {
        get;
        set => field = value ?? HierarchyNamespaceOptions.None;
    } = HierarchyNamespaceOptions.None;
}
