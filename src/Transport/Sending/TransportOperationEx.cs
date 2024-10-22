namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// TransportOperation extension methods
/// </summary>
public static class TransportOperationExt
{
    internal const string DisableLegacyTransportCompatibilityHeadersKey = "DisableLegacyTransportCompatibility";

    /// <summary>
    /// Disable generation of `NServiceBus.Transport.Encoding` header for backwards compatibility with "NServiceBus.AzureServiceBus"
    /// </summary>
    public static void DisableLegacyTransportCompatibility(this TransportOperation instance)
    {
        instance.Properties.Add(DisableLegacyTransportCompatibilityHeadersKey, bool.TrueString);
    }
}