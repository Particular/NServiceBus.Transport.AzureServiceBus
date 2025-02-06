#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "The subscription name for a given subscriber queue can be overriden on the topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "OverrideSubscriptionNameFor")]
        public Func<string, string> SubscriptionNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "The subscription rule name can be overriden on the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "OverrideRuleNameFor")]
        public Func<Type, string> SubscriptionRuleNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(Message = "Selecting the transport requires to choose a topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "UseTransport(topology)")]
        public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration)
            where TTransport : AzureServiceBusTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(Message = "Setting the topic name is accessible via the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "Topology.Single(topicName)")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName) => throw new NotImplementedException();

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