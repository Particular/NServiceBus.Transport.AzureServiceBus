#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "The transport no longer supports sending the transport encoding header.",
        TreatAsErrorFromVersion = "6",
        RemoveInVersion = "7")]
        public bool SendTransportEncodingHeader
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message = "The transport no longer supports sending the transport encoding header.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<AzureServiceBusTransport> SendTransportEncodingHeader(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions) => throw new NotImplementedException();
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member