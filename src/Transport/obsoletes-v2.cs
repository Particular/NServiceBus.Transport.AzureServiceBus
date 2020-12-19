#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Transport.AzureServiceBus;

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Specifies a callback to apply to the subscription name when the endpoint's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions">The transport settings object</param>
        /// <param name="subscriptionNameShortener">The callback to apply.</param>
        // TODO: remove when cherry-picked into release-1.x
        [ObsoleteEx(Message = "Use `SubscriptionNameConvention()` instead.",
            RemoveInVersion = "3",
            TreatAsErrorFromVersion = "2")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNameShortener)
        {
            Guard.AgainstNull(nameof(subscriptionNameShortener), subscriptionNameShortener);

            Func<string, string> wrappedSubscriptionNameShortener = subsciptionName =>
            {
                try
                {
                    return subscriptionNameShortener(subsciptionName);
                }
                catch (Exception exception)
                {
                    throw new Exception("Custom subscription name shortener threw an exception.", exception);
                }
            };

            transportExtensions.GetSettings().Set(SettingsKeys.SubscriptionNameShortener, wrappedSubscriptionNameShortener);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies a callback to apply to a subscription rule name when a subscribed event's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions">The transport settings object</param>
        /// <param name="ruleNameShortener">The callback to apply.</param>
        // TODO: remove when cherry-picked into release-1.x
        [ObsoleteEx(Message = "Use `RuleNameConvention()` instead.",
            RemoveInVersion = "3",
            TreatAsErrorFromVersion = "2")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> ruleNameShortener)
        {
            Guard.AgainstNull(nameof(ruleNameShortener), ruleNameShortener);

            Func<string, string> wrappedRuleNameShortener = ruleName =>
            {
                try
                {
                    return ruleNameShortener(ruleName);
                }
                catch (Exception exception)
                {
                    throw new Exception("Custom rule name shortener threw an exception.", exception);
                }
            };

            transportExtensions.GetSettings().Set(SettingsKeys.RuleNameShortener, wrappedRuleNameShortener);

            return transportExtensions;
        }
    }
}

#pragma warning restore 1591