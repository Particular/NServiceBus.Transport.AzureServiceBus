#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "Next versions of the transport will no longer support sending the transport encoding header.",
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
            Message = "Next versions of the transport will no longer support sending the transport encoding header.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<AzureServiceBusTransport> SendTransportEncodingHeader(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.Transport.SendTransportEncodingHeader = true;
            return transportExtensions;
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member