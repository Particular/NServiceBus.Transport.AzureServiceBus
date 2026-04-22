namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Configures the shared fallback topic used for unmapped events.
/// </summary>
public sealed class FallbackTopicOptions
{
    /// <summary>
    /// Name of the topic used for unmapped events.
    /// </summary>
    public string? TopicName { get; set; }

    /// <summary>
    /// Routing mode used for the fallback topic.
    /// </summary>
    public TopicRoutingMode Mode { get; set; } = TopicRoutingMode.Default;
}
