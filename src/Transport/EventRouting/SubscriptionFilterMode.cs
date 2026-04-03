namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Specifies the filter mode for shared-topic subscription filtering.
/// </summary>
public enum SubscriptionFilterMode
{
    /// <summary>
    /// Uses CorrelationFilter for exact-match filtering on application properties.
    /// </summary>
    CorrelationFilter,

    /// <summary>
    /// Uses SqlFilter for LIKE-based filtering on the EnclosedMessageTypes header.
    /// </summary>
    SqlFilter,

    /// <summary>
    /// Receives all messages on the shared topic without filtering.
    /// </summary>
    CatchAll
}