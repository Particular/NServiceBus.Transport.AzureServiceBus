#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "The subscription name can be set on the topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology")]
        public Func<string, string> SubscriptionNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "The subscription rule name can be set on the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology")]
        public Func<Type, string> SubscriptionRuleNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(Message = "TBD",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "TBD",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNamingConvention)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "TBD",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<Type, string> subscriptionRuleNamingConvention)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member