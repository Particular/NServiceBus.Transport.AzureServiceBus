#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNameShortener)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> ruleNameShortener)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591
