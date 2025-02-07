#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;
    using Azure.Core;

    public partial class AzureServiceBusTransport
    {
        [ObsoleteEx(Message = "It is required to choose a topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "AzureServiceBusTransport(string connectionString, TopicTopology topology)")]
        public AzureServiceBusTransport(string connectionString) : base(
            TransportTransactionMode.SendsAtomicWithReceive,
            true,
            true,
            true) =>
            throw new NotImplementedException();

        [ObsoleteEx(Message = "It is required to choose a topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember =
                "AzureServiceBusTransport(string fullyQualifiedNamespace, TokenCredential tokenCredential, TopicTopology topology)")]
        public AzureServiceBusTransport(string fullyQualifiedNamespace, TokenCredential tokenCredential) : base(
            TransportTransactionMode.SendsAtomicWithReceive,
            true,
            true,
            true) =>
            throw new NotImplementedException();

        [ObsoleteEx(Message = "Setting the subscription name is accessible via the topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "OverrideSubscriptionNameFor")]
        public Func<string, string> SubscriptionNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [ObsoleteEx(Message = "Setting the subscription rule name is accessible via the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "MigrationTopology.EventToMigrate<TEventType>(string? ruleNameOverride = null)")]
        public Func<Type, string> SubscriptionRuleNamingConvention
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message =
                "The transport by default no longer sends the transport encoding header for wire compatibility with NServiceBus.AzureServiceBus, requiring an opt-in for the header to be sent.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "SendTransportEncodingHeader")]
        public bool DoNotSendTransportEncodingHeader
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(
            Message =
                "Selecting the transport requires to choose a topology. Use one of the UseTransport overloads that accepts a topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6")]
        public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration)
            where TTransport : AzureServiceBusTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(Message = "Setting the topic name is accessible via the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "TopicTopology.MigrateFromNamedSingleTopic(topicName)")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName) => throw new NotImplementedException();

        [ObsoleteEx(Message = "Setting the subscription name is accessible via the topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "OverrideSubscriptionNameFor")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> subscriptionNamingConvention) => throw new NotImplementedException();

        [ObsoleteEx(Message = "Setting the subscription rule name is accessible via the migration topology.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6",
            ReplacementTypeOrMember = "MigrationTopology.EventToMigrate<TEventType>(string? ruleNameOverride = null)")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<Type, string> subscriptionRuleNamingConvention) => throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "The transport by default no longer sends the transport encoding header for wire compatibility with NServiceBus.AzureServiceBus, requiring an opt-in for the header to be sent.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7",
            ReplacementTypeOrMember =
                "SendTransportEncodingHeader(this TransportExtensions<AzureServiceBusTransport> transportExtensions)")]
        public static TransportExtensions<AzureServiceBusTransport> DoNotSendTransportEncodingHeader(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions) =>
            throw new NotImplementedException();
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member