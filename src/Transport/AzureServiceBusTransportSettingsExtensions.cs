﻿namespace NServiceBus
{
    using System;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.Transport.AzureServiceBus;

    /// <summary>
    /// Adds access to the Azure Service Bus transport config to the global Transport object.
    /// </summary>
    public static class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Configure the endpoint to use the Azure Service bus transport. This configuration method will eventually be deprecated.
        /// Consider using endpointConfiguration.UseTransport(new AzureServiceBusTransport(connectionString)) instead.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration)
            where TTransport : AzureServiceBusTransport
        {
            var transport = new AzureServiceBusTransport();

            var routing = endpointConfiguration.UseTransport(transport);

            return new TransportExtensions<AzureServiceBusTransport>(transport, routing);
        }

        /// <summary>
        /// Sets the Azure Service Bus connection string.
        /// </summary>
        [PreObsolete(
                    Message = "Provide the connection string to the AzureServiceBusTransport constructor",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            transportExtensions.Transport.ConnectionString = connectionString;
            return transportExtensions;
        }

        /// <summary>
        /// Sets the Azure Service Bus connection string.
        /// </summary>
        [PreObsolete(
                    Message = "Provide the connection string to the AzureServiceBusTransport constructor",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string> connectionString)
        {
            Guard.AgainstNull(nameof(connectionString), connectionString);
            var value = connectionString();
            Guard.AgainstNullAndEmpty(nameof(connectionString), value);
            transportExtensions.Transport.ConnectionString = value;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default topic name used to publish events between endpoints.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="topicName">The name of the topic used to publish events between endpoints.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TopicName",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> TopicName(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string topicName)
        {
            Guard.AgainstNullAndEmpty(nameof(topicName), topicName);
            transportExtensions.Transport.TopicName = topicName;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default maximum size used when creating queues and topics.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maximumSizeInGB">The maximum size to use, in gigabytes.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> EntityMaximumSize(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int maximumSizeInGB)
        {
            Guard.AgainstNegativeAndZero(nameof(maximumSizeInGB), maximumSizeInGB);
            transportExtensions.Transport.EntityMaximumSize = maximumSizeInGB;
            return transportExtensions;
        }

        /// <summary>
        /// Enables entity partitioning when creating queues and topics.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.EnablePartitioning",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> EnablePartitioning(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.Transport.EnablePartitioning = true;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchMultiplier(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchMultiplier)
        {
            Guard.AgainstNegativeAndZero(nameof(prefetchMultiplier), prefetchMultiplier);
            transportExtensions.Transport.PrefetchMultiplier = prefetchMultiplier;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> PrefetchCount(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int prefetchCount)
        {
            Guard.AgainstNegative(nameof(prefetchCount), prefetchCount);
            transportExtensions.Transport.PrefetchCount = prefetchCount;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump cannot successfully receive a message.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="timeToWait">The time to wait before triggering the circuit breaker.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan timeToWait)
        {
            Guard.AgainstNegativeAndZero(nameof(timeToWait), timeToWait);
            transportExtensions.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = timeToWait;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies a callback to customize subscription names.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionNamingConvention">The callback to apply.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string, string> subscriptionNamingConvention)
        {
            Guard.AgainstNull(nameof(subscriptionNamingConvention), subscriptionNamingConvention);
            transportExtensions.Transport.SubscriptionNamingConvention = subscriptionNamingConvention;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies a callback to customize subscription rule names.
        /// </summary>
        /// <remarks>The naming convention callback is called for every subscribed event <see cref="Type"/>.</remarks>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionRuleNamingConvention">The callback to apply.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionRuleNamingConvention(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<Type, string> subscriptionRuleNamingConvention)
        {
            Guard.AgainstNull(nameof(subscriptionRuleNamingConvention), subscriptionRuleNamingConvention);
            transportExtensions.Transport.SubscriptionRuleNamingConvention = subscriptionRuleNamingConvention;
            return transportExtensions;
        }

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        /// <param name="transportExtensions"></param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.Transport.UseWebSockets = true;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default retry policy with a custom implementation.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="retryPolicy">A custom retry policy to be used.</param>
        [PreObsolete(
            ReplacementTypeOrMember = "AzureServiceBusTransport.RetryPolicyOptions",
            TreatAsErrorFromVersion = "4",
            RemoveInVersion = "5")]
        public static TransportExtensions<AzureServiceBusTransport> CustomRetryPolicy(this TransportExtensions<AzureServiceBusTransport> transportExtensions, ServiceBusRetryOptions retryPolicy)
        {
            Guard.AgainstNull(nameof(retryPolicy), retryPolicy);
            transportExtensions.Transport.RetryPolicyOptions = retryPolicy;
            return transportExtensions;
        }
    }
}