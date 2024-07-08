namespace NServiceBus.Transport.AzureServiceBus.Experimental;

using System;

/// <summary>
/// TransportOperation extensions
/// </summary>
public static class TransportOperationExt
{
    internal const string DisableLegacyHeadersKey = "DisableLegacyHeaders";

    /// <summary>
    /// Disable adding legacy NServiceBus headers for backwards compatibility with NServiceBus.AzureServiceBus (ASBL)
    /// </summary>
    /// <param name="instance"></param>
    [Obsolete("This is experimental")]
    [DoNotWarnAboutObsoleteUsage]
    public static void DisableLegacyHeaders(this TransportOperation instance)
    {
        instance.Properties.Add(DisableLegacyHeadersKey, bool.TrueString);
    }
}