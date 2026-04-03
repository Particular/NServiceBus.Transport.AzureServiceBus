namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Controls whether and how events are multiplexed when multiple event types share a topic.
/// </summary>
public enum PublishMultiplexingMode
{
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
