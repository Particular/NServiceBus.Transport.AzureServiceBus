namespace NServiceBus
{
    using System;
    using System.Net;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Adds access to the Azure Service Bus transport config to the global Transport object.
    /// </summary>
    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Configure the endpoint to use the Azure Service bus transport. This configuration method will eventually be deprecated.
        /// Consider using endpointConfiguration.UseTransport(new AzureServiceBusTransport(connectionString)) instead.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            Message = "Provide the connection string to the AzureServiceBusTransport constructor")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            transportExtensions.Transport.ConnectionString = connectionString;
            return transportExtensions;
        }

        /// <summary>
        /// Sets the Azure Service Bus connection string.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            Message = "Provide the connection string to the AzureServiceBusTransport constructor")]
        public static TransportExtensions<AzureServiceBusTransport> ConnectionString(this TransportExtensions<AzureServiceBusTransport> transportExtensions, Func<string> connectionString)
        {
            Guard.AgainstNull(nameof(connectionString), connectionString);
            var value = connectionString();
            Guard.AgainstNullAndEmpty(nameof(connectionString), value);
            transportExtensions.Transport.ConnectionString = value;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default maximum size used when creating queues and topics.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maximumSizeInGB">The maximum size to use, in gigabytes.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.EntityMaximumSize")]
        public static TransportExtensions<AzureServiceBusTransport> EntityMaximumSize(this TransportExtensions<AzureServiceBusTransport> transportExtensions, int maximumSizeInGB)
        {
            Guard.AgainstNegativeAndZero(nameof(maximumSizeInGB), maximumSizeInGB);
            transportExtensions.Transport.EntityMaximumSize = maximumSizeInGB;
            return transportExtensions;
        }

        /// <summary>
        /// Enables entity partitioning when creating queues and topics.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.EnablePartitioning")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchMultiplier")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.PrefetchCount")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.TimeToWaitBeforeTriggeringCircuitBreaker")]
        public static TransportExtensions<AzureServiceBusTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan timeToWait)
        {
            Guard.AgainstNegativeAndZero(nameof(timeToWait), timeToWait);
            transportExtensions.Transport.TimeToWaitBeforeTriggeringCircuitBreaker = timeToWait;
            return transportExtensions;
        }

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="webProxy">The proxy to use for communication over web sockets.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.UseWebSockets")]
        public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions, IWebProxy webProxy = default)
        {
            transportExtensions.Transport.UseWebSockets = true;
            if (webProxy != default)
            {
                transportExtensions.Transport.WebProxy = webProxy;
            }
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default retry policy with a custom implementation.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="retryPolicy">A custom retry policy to be used.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.RetryPolicyOptions")]
        public static TransportExtensions<AzureServiceBusTransport> CustomRetryPolicy(this TransportExtensions<AzureServiceBusTransport> transportExtensions, ServiceBusRetryOptions retryPolicy)
        {
            Guard.AgainstNull(nameof(retryPolicy), retryPolicy);
            transportExtensions.Transport.RetryPolicyOptions = retryPolicy;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default token credential with a custom one.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="fullyQualifiedNamespace">The fully qualified namespace.</param>
        /// <param name="tokenCredential">The token credential to be used.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport(string fullyQualifiedNamespace, TokenCredential tokenCredential)")]
        public static TransportExtensions<AzureServiceBusTransport> CustomTokenCredential(this TransportExtensions<AzureServiceBusTransport> transportExtensions, string fullyQualifiedNamespace, TokenCredential tokenCredential)
        {
            Guard.AgainstNull(nameof(tokenCredential), tokenCredential);
            Guard.AgainstNullAndEmpty(nameof(fullyQualifiedNamespace), fullyQualifiedNamespace);
            transportExtensions.Transport.FullyQualifiedNamespace = fullyQualifiedNamespace;
            transportExtensions.Transport.TokenCredential = tokenCredential;
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default maximum duration within which the lock will be renewed automatically. This
        /// value should be greater than the longest message lock duration.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maximumAutoLockRenewalDuration">The maximum duration during which message locks are automatically renewed. The default value is 5 minutes.</param>
        /// <remarks>The message renew can continue for sometime in the background
        /// after completion of message and result in a few false MessageLockLostExceptions temporarily.</remarks>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
            ReplacementTypeOrMember = "AzureServiceBusTransport.MaxAutoLockRenewalDuration")]
        public static TransportExtensions<AzureServiceBusTransport> MaxAutoLockRenewalDuration(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TimeSpan maximumAutoLockRenewalDuration)
        {
            Guard.AgainstNegative(nameof(maximumAutoLockRenewalDuration), maximumAutoLockRenewalDuration);
            transportExtensions.Transport.MaxAutoLockRenewalDuration = maximumAutoLockRenewalDuration;
            return transportExtensions;
        }

        /// <summary>
        /// When set will not add `NServiceBus.Transport.Encoding` header for wire compatibility with NServiceBus.AzureServiceBus. The default value is <c>false</c>.
        /// </summary>
        /// <param name="transportExtensions"></param>
        [ObsoleteEx(Message = "Next versions of the transport will by default no longer send the transport encoding header for wire compatibility, requiring an opt-in for the header to be sent.",
            TreatAsErrorFromVersion = "5",
            RemoveInVersion = "6")]
        public static TransportExtensions<AzureServiceBusTransport> DoNotSendTransportEncodingHeader(this TransportExtensions<AzureServiceBusTransport> transportExtensions)
        {
            transportExtensions.Transport.DoNotSendTransportEncodingHeader = true;
            return transportExtensions;
        }
    }
}