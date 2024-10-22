namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// TransportOperation extension methods
/// </summary>
public static class TransportOperationExt
{
    internal const string DoNotSendTransportEncodingHeaderKey = "DoNotSendTransportEncodingHeader";

    /// <summary>
    /// Disable generation of `NServiceBus.Transport.Encoding` header for backwards compatibility with "NServiceBus.AzureServiceBus"
    /// </summary>
    public static void DoNotSendTransportEncodingHeader(this TransportOperation instance)
    {
        instance.Properties.Add(DoNotSendTransportEncodingHeaderKey, bool.TrueString);
    }
}