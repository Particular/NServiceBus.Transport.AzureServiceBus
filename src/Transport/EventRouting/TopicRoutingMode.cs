namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Controls how events are routed and filtered when multiple event types share a topic.
/// </summary>
public enum TopicRoutingMode
{
    /// <summary>
    /// Inherits the effective routing mode from the calling context.
    /// </summary>
    Default,

    /// <summary>
    /// Publishes or subscribes without multiplexing-specific behavior.
    /// </summary>
    NotMultiplexed,

    /// <summary>
    /// Uses CorrelationFilter-compatible routing behavior.
    /// </summary>
    CorrelationFilter,

    /// <summary>
    /// Uses SqlFilter-compatible routing behavior.
    /// </summary>
    SqlFilter,

    /// <summary>
    /// Receives all messages on the shared topic without filtering.
    /// </summary>
    CatchAll
}
