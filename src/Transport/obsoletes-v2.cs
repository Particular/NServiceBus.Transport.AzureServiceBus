#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Specifies a callback to apply to the subscription name when the endpoint's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions">The transport settings object</param>
        /// <param name="subscriptionNameShortener">The callback to apply.</param>
        [ObsoleteEx(Message = "Use `SubscriptionNameConvention()` instead.",
            RemoveInVersion = "3",
            TreatAsErrorFromVersion = "2")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNameShortener)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Specifies a callback to apply to a subscription rule name when a subscribed event's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions">The transport settings object</param>
        /// <param name="ruleNameShortener">The callback to apply.</param>
        [ObsoleteEx(Message = "Use `RuleNameConvention()` instead.",
            RemoveInVersion = "3",
            TreatAsErrorFromVersion = "2")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> ruleNameShortener)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591
