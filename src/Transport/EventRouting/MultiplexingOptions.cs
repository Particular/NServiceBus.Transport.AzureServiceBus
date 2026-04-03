namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Configuration options for shared-topic multiplexing on the publish side.
/// </summary>
public sealed class MultiplexingOptions
{
    /// <summary>
    /// Controls whether multiplexing is used and which publish-side behavior applies.
    /// </summary>
    public PublishMultiplexingMode Mode { get; set; } = PublishMultiplexingMode.Default;
}
