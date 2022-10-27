#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace NServiceBus
{
    using System;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;

    public static class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration)
            where TTransport : AzureServiceBusTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(
                    Message = "Provide the connection string to the AzureServiceBusTransport constructor",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string connectionString)
            => throw new NotImplementedException();

        [ObsoleteEx(
                    Message = "Provide the connection string to the AzureServiceBusTransport constructor",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string> connectionString)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TopicName",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> EntityMaximumSize(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int maximumSizeInGB)
            => throw new NotImplementedException();

        public static TransportExtensions<AzureServiceBusTransport> EnablePartitioning(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchMultiplier(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchMultiplier)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchCount(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchCount)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan timeToWait)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNamingConvention)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<Type, string> subscriptionRuleNamingConvention)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.RetryPolicyOptions",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> CustomRetryPolicy(this TransportExtensions<AzureServiceBusTransport> transportExtensions, ServiceBusRetryOptions retryPolicy)
            => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TokenCredential",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> CustomTokenCredential(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TokenCredential tokenCredential)
            => throw new NotImplementedException();
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member