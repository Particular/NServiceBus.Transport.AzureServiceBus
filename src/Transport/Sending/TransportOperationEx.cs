namespace NServiceBus.Transport.AzureServiceBus.Experimental;

using System;

[Obsolete("Experimental")]
public static class TransportOperationExt
{
    internal const string DisableLegacyHeadersKey = "DisableLegacyHeaders";

    public static void DisableLegacyHeaders(this TransportOperation instance)
    {
        instance.Properties.Add(DisableLegacyHeadersKey, bool.TrueString);
    }
}