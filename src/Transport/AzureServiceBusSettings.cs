namespace NServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Provides a backwards-compatible configuration API for <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    public class AzureServiceBusSettings : TransportSettings<AzureServiceBusTransport>
    {
        internal AzureServiceBusSettings(AzureServiceBusTransport transport, RoutingSettings<AzureServiceBusTransport> routing) : base(transport, routing)
        {
        }

        /// <summary>
        /// Configures the transport to use the connection string with the given name.
        /// </summary>
        [ObsoleteEx(
            Message = "Provide the connection string to the AzureServiceBusTransport constructor",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings ConnectionString(string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            Transport.ConnectionString = connectionString;
            return this;
        }

        /// <summary>
        /// Configures the transport to use the connection string with the given name.
        /// </summary>
        [ObsoleteEx(
            Message =
                "The ability to used named connection strings has been removed. Instead, load the connection string in your code and pass the value to TransportExtensions.ConnectionString(connectionString)",
            ReplacementTypeOrMember = "TransportExtensions.ConnectionString(connectionString)",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public AzureServiceBusSettings ConnectionStringName(string name) => throw new NotImplementedException();

        /// <summary>
        /// Configures the transport to use the given func as the connection string.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Setting connection string at the endpoint level is no longer supported. Transport specific configuration options should be used instead",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public AzureServiceBusSettings ConnectionString(Func<string> connectionString) =>
            throw new NotImplementedException();

        /// <summary>
        /// Specifies a callback to apply to the subscription name when the endpoint's name is longer than 50 characters.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public AzureServiceBusSettings SubscriptionNameShortener(Func<string, string> subscriptionNameShortener) =>
            throw new NotImplementedException();

        /// <summary>
        /// Specifies a callback to apply to a subscription rule name when a subscribed event's name is longer than 50 characters.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "2",
            RemoveInVersion = "3")]
        public AzureServiceBusSettings RuleNameShortener(Func<string, string> ruleNameShortener) =>
            throw new NotImplementedException();

        /// <summary>
        /// Overrides the default topic name used to publish events between endpoints.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TopicName",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings TopicName(string topicName)
        {
            Transport.TopicName = topicName;
            return this;
        }

        /// <summary>
        /// Overrides the default maximum size used when creating queues and topics.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings EntityMaximumSize(int maximumSizeInGB)
        {
            Transport.EntityMaximumSize = maximumSizeInGB;
            return this;
        }

        /// <summary>
        /// Enables entity partitioning when creating queues and topics.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EnablePartitioning",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings EnablePartitioning()
        {
            Transport.EnablePartitioning = true;
            return this;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings PrefetchMultiplier(int prefetchMultiplier)
        {
            Transport.PrefetchMultiplier = prefetchMultiplier;
            return this;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>s
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings PrefetchCount(int prefetchCount)
        {
            Transport.PrefetchCount = prefetchCount;
            return this;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump cannot successfully receive a message.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan timeToWait)
        {
            Transport.TimeToWaitBeforeTriggeringCircuitBreaker = timeToWait;
            return this;
        }

        /// <summary>
        /// Specifies a callback to customize subscription names.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings SubscriptionNamingConvention(Func<string, string> subscriptionNamingConvention)
        {
            Transport.SubscriptionNamingConvention = subscriptionNamingConvention;
            return this;
        }

        /// <summary>
        /// Specifies a callback to customize subscription rule names.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings SubscriptionRuleNamingConvention(Func<Type, string> subscriptionRuleNamingConvention)
        {
            Transport.SubscriptionRuleNamingConvention = subscriptionRuleNamingConvention;
            return this;
        }

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings UseWebSockets()
        {
            Transport.UseWebSockets = true;
            return this;
        }

        /// <summary>
        /// Overrides the default token provider with a custom implementation.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TokenProvider",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings CustomTokenProvider(ITokenProvider tokenProvider)
        {
            Transport.TokenProvider = tokenProvider;
            return this;
        }

        /// <summary>
        /// Overrides the default retry policy with a custom implementation.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AzureServiceBusTransport.RetryPolicy",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public AzureServiceBusSettings CustomRetryPolicy(RetryPolicy retryPolicy)
        {
            Transport.RetryPolicy = retryPolicy;
            return this;
        }
    }
}