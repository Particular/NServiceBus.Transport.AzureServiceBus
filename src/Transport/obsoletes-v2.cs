#pragma warning disable 1591
#pragma warning disable 618

namespace NServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;

    public static class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNameShortener)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SubscriptionRuleNamingConvention",
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

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TokenProvider",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> CustomTokenProvider(this TransportExtensions<AzureServiceBusTransport> transportExtensions, ITokenProvider tokenProvider) => throw new NotImplementedException();

        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.RetryPolicy",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> CustomRetryPolicy(this TransportExtensions<AzureServiceBusTransport> transportExtensions, RetryPolicy retryPolicy) => throw new NotImplementedException();
    }
}

namespace NServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using Extensibility;

    public static partial class CustomizeNativeMessageExtensions
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "CustomizeNativeMessage(this ExtendableOptions options, Action<Message> customization)",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static void CustomizeNativeMessage(this ExtendableOptions options, IPipelineContext context,
            Action<Message> customization) => throw new NotImplementedException();
    }
}

#pragma warning restore 1591
#pragma warning restore 618
