namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Controls whether and how events are multiplexed when multiple event types share a topic.
/// </summary>
public enum PublishMultiplexingMode
{
    /// <summary>
    /// Inherits the topology-wide default publish multiplexing mode.
    /// </summary>
    Default,

    /// <summary>
    /// Publishes without multiplexing-specific stamping.
    /// </summary>
    NotMultiplexed,

    /// <summary>
    /// Publishes with exact-match correlation stamping.
    /// </summary>
    MultiplexedUsingCorrelationFilter,

    /// <summary>
    /// Publishes for SQL-filter-based subscriptions without additional correlation stamping.
    /// </summary>
    MultiplexedUsingSqlFilter
}
