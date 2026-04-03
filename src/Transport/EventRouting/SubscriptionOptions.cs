namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Configuration options for subscriptions with shared-topic filtering.
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>
    /// The filter mode to use for subscription filtering.
    /// </summary>
    public SubscriptionFilterMode FilterMode { get; set; } = SubscriptionFilterMode.Default;
}
