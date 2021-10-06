#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    public static class AzureServiceBusSettingsExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static AzureServiceBusSettings UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration)
        {
            throw new NotImplementedException();
        }
    }
}

namespace NServiceBus
{
    using System;

    public class AzureServiceBusSettings
    {
        [ObsoleteEx(
                    Message = "Provide the connection string to the AzureServiceBusTransport constructor",
                    TreatAsErrorFromVersion = "3",
                    RemoveInVersion = "4")]
        public AzureServiceBusSettings ConnectionString(string connectionString)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TopicName",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings TopicName(string topicName)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings EntityMaximumSize(int maximumSizeInGB)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EnablePartitioning",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings EnablePartitioning()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings PrefetchMultiplier(int prefetchMultiplier)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings PrefetchCount(int prefetchCount)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan timeToWait)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings SubscriptionNamingConvention(Func<string, string> subscriptionNamingConvention)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings SubscriptionRuleNamingConvention(Func<Type, string> subscriptionRuleNamingConvention)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings UseWebSockets()
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591