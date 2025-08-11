#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus;

using System;
using Particular.Obsoletes;

public partial class AzureServiceBusTransport
{
    [ObsoleteMetadata(Message = "The transport no longer supports sending the transport encoding header",
    TreatAsErrorFromVersion = "6",
    RemoveInVersion = "7")]
    [Obsolete("The transport no longer supports sending the transport encoding header. Will be removed in version 7.0.0.", true)]
    public bool SendTransportEncodingHeader
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}

public static partial class AzureServiceBusTransportSettingsExtensions
{
    [ObsoleteMetadata(
        Message = "The transport no longer supports sending the transport encoding header",
        TreatAsErrorFromVersion = "6",
        RemoveInVersion = "7")]
    [Obsolete("The transport no longer supports sending the transport encoding header. Will be removed in version 7.0.0.", true)]
    public static TransportExtensions<AzureServiceBusTransport> SendTransportEncodingHeader(
        this TransportExtensions<AzureServiceBusTransport> transportExtensions) => throw new NotImplementedException();
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member