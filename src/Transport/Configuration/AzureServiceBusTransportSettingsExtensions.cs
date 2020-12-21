namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Adds access to the Azure Service Bus transport config to the global Transport object.
    /// </summary>
    public static class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Overrides the default topic name used to publish events between endpoints.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="topicName">The name of the topic used to publish events between endpoints.</param>
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName)
        {
            Guard.AgainstNullAndEmpty(nameof(topicName), topicName);

            transportExtensions.GetSettings().Set(SettingsKeys.TopicName, topicName);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default maximum size used when creating queues and topics.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maximumSizeInGB">The maximum size to use, in gigabytes.</param>
        public static TransportExtensions<AzureServiceBusTransport> EntityMaximumSize(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int maximumSizeInGB)
        {
            Guard.AgainstNegativeAndZero(nameof(maximumSizeInGB), maximumSizeInGB);

            transportExtensions.GetSettings().Set(SettingsKeys.MaximumSizeInGB, maximumSizeInGB);

            return transportExtensions;
        }

        /// <summary>
        /// Enables entity partitioning when creating queues and topics.
        /// </summary>
        public static TransportExtensions<AzureServiceBusTransport> EnablePartitioning(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.EnablePartitioning, true);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        public static TransportExtensions<AzureServiceBusTransport> PrefetchMultiplier(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchMultiplier)
        {
            Guard.AgainstNegativeAndZero(nameof(prefetchMultiplier), prefetchMultiplier);

            transportExtensions.GetSettings().Set(SettingsKeys.PrefetchMultiplier, prefetchMultiplier);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        public static TransportExtensions<AzureServiceBusTransport> PrefetchCount(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchCount)
        {
            Guard.AgainstNegative(nameof(prefetchCount), prefetchCount);

            transportExtensions.GetSettings().Set(SettingsKeys.PrefetchCount, prefetchCount);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump cannot successfully receive a message.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="timeToWait">The time to wait before triggering the circuit breaker.</param>
        public static TransportExtensions<AzureServiceBusTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan timeToWait)
        {
            Guard.AgainstNegativeAndZero(nameof(timeToWait), timeToWait);

            transportExtensions.GetSettings().Set(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, timeToWait);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies a callback to apply to the subscription name when the endpoint's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionNameShortener">The callback to apply.</param>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
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
        /// <param name="transportExtensions"></param>
        /// <param name="ruleNameShortener">The callback to apply.</param>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
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

        /// <summary>
        /// Specifies a callback to customize subscription names.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionNamingConvention">The callback to apply.</param>
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNamingConvention)
        {
            Guard.AgainstNull(nameof(subscriptionNamingConvention), subscriptionNamingConvention);

            Func<string, string> wrappedSubscriptionNamingConvention = name =>
            {
                try
                {
                    return subscriptionNamingConvention(name);
                }
                catch (Exception exception)
                {
                    throw new Exception("Custom subscription naming convention threw an exception.", exception);
                }
            };

            transportExtensions.GetSettings().Set(SettingsKeys.SubscriptionNamingConvention, wrappedSubscriptionNamingConvention);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies a callback to customize subscription rule names for event types.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionRuleNamingConvention">The callback to apply.</param>
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<Type, string> subscriptionRuleNamingConvention)
        {
            Guard.AgainstNull(nameof(subscriptionRuleNamingConvention), subscriptionRuleNamingConvention);

            Func<Type, string> wrappedSubscriptionRuleNamingConvention = eventType =>
            {
                try
                {
                    return subscriptionRuleNamingConvention(eventType);
                }
                catch (Exception exception)
                {
                    throw new Exception("Custom subscription rule naming convention threw an exception.", exception);
                }
            };

            transportExtensions.GetSettings().Set(SettingsKeys.SubscriptionRuleNamingConvention, wrappedSubscriptionRuleNamingConvention);

            return transportExtensions;
        }

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        /// <param name="transportExtensions"></param>
        public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.TransportType, TransportType.AmqpWebSockets);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default token provider with a custom implementation.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="tokenProvider">The token provider to be used.</param>
        public static TransportExtensions<AzureServiceBusTransport> CustomTokenProvider(this TransportExtensions<AzureServiceBusTransport> transportExtensions, ITokenProvider tokenProvider)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.CustomTokenProvider, tokenProvider);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default retry policy with a custom implementation.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="retryPolicy">A custom retry policy to be used.</param>
        public static TransportExtensions<AzureServiceBusTransport> CustomRetryPolicy(this TransportExtensions<AzureServiceBusTransport> transportExtensions, RetryPolicy retryPolicy)
        {
            Guard.AgainstNull(nameof(retryPolicy), retryPolicy);

            transportExtensions.GetSettings().Set(SettingsKeys.CustomRetryPolicy, retryPolicy);

            return transportExtensions;
        }
    }
}