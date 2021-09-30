#pragma warning disable 1591
#pragma warning disable 618

namespace NServiceBus
{
    using System;

    public static class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNameShortener)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> ruleNameShortener)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TopicName",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> EntityMaximumSize(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int maximumSizeInGB) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EnablePartitioning",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> EnablePartitioning(this TransportExtensions<AzureServiceBusTransport> transportExtensions) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchMultiplier(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchMultiplier) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchCount(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchCount) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan timeToWait) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> subscriptionNamingConvention) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<Type, string> subscriptionRuleNamingConvention) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions) => throw new NotImplementedException();
    }
}

namespace NServiceBus
{
    using System;

    public partial class AzureServiceBusTransport
    {
        // Used by the shim API to enable easier migration from the existing API
        protected internal AzureServiceBusTransport() : base(
            defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            supportsDelayedDelivery: true,
            supportsPublishSubscribe: true,
            supportsTTBR: true)
        {
        }

        void CheckConnectionStringHasBeenConfigured()
        {
            // Check if a connection string has been provided when using the shim configuration API.
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception(
                    "No transport connection string has been configured via the 'ConnectionString' method. Provide a connection string using 'endpointConfig.UseTransport<AzureServiceBusTransport>().ConnectionString(connectionString)'.");
            }
        }
    }
}

#pragma warning restore 1591
#pragma warning restore 618
