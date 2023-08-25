#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;
    using NServiceBus.Transport;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address) => throw new NotImplementedException();
#pragma warning restore CS0672 // Member overrides obsolete member

        [ObsoleteEx(Message = "It is possible to represent the publish and subscribe topic separately by specifying a topology.",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5",
            ReplacementTypeOrMember = "Topology")]
        public string TopicName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member