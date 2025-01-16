#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        // TODO This needs to go
        [ObsoleteEx(Message = "It is possible to represent the publish and subscribe topic separately by specifying a topology.",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5",
            ReplacementTypeOrMember = "Topology")]
        public string TopicName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "The subscription name can be set on the topology.",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5",
            ReplacementTypeOrMember = "Topology")]
        public Func<string, string> SubscriptionNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member