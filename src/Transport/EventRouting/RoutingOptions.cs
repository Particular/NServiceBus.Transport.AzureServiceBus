namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Configuration options for shared-topic routing.
/// </summary>
public sealed class RoutingOptions
{
    /// <summary>
    /// Controls the routing behavior to use.
    /// </summary>
    public TopicRoutingMode Mode { get; set; } = TopicRoutingMode.Default;
}
