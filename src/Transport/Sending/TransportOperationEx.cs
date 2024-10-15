namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// TransportOperation extension methods
/// </summary>
public static class TransportOperationExt
{
    internal const string DisableLegacyHeadersKey = "DisableLegacyHeaders";

    /// <summary>
    /// Disable generation of `NServiceBus.Transport.Encoding` header for backwards compatibility with "NServiceBus.AzureServiceBus"
    /// </summary>
    public static void DisableLegacyHeaders(this TransportOperation instance)
    {
        instance.Properties.Add(DisableLegacyHeadersKey, bool.TrueString);
    }
}