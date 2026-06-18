namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// Configures the shared fallback topic used when an event or subscription has no explicit topic mapping.
/// When configured, otherwise-unmapped events are routed to this topic instead of being treated as unmapped.
/// </summary>
public sealed class FallbackTopicOptions : IHierarchyNamespaceAwareOptions
{
    /// <summary>
    /// Name of the topic used for otherwise-unmapped events and subscriptions.
    /// </summary>
    [AzureServiceBusTopics]
    public string? TopicName { get; set; }

    /// <summary>
    /// Routing mode used for the fallback topic.
    /// When a fallback topic is configured, this also means <see cref="TopologyOptions.ThrowIfUnmappedEventTypes"/>
    /// no longer throws for events resolved through this fallback topic.
    /// </summary>
    [FallbackTopicMode]
    public TopicRoutingMode? RoutingMode { get; set; }

    [JsonIgnore]
    internal HierarchyNamespaceOptions HierarchyOptions
    {
        get;
        set => field = value ?? HierarchyNamespaceOptions.None;
    } = HierarchyNamespaceOptions.None;

    [JsonIgnore]
    HierarchyNamespaceOptions IHierarchyNamespaceAwareOptions.HierarchyNamespaceOptions
    {
        get => HierarchyOptions;
        set => HierarchyOptions = value;
    }
}