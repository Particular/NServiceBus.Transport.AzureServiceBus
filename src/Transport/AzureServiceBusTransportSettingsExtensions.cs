namespace NServiceBus;

using System;
using System.Net;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Particular.Obsoletes;

/// <summary>
/// Adds access to the Azure Service Bus transport config to the global Transport object.
/// </summary>
public static partial class AzureServiceBusTransportSettingsExtensions
{
    /// <summary>
    /// Configure the endpoint to use the Azure Service bus transport. This configuration method will eventually be deprecated.
    /// Consider using endpointConfiguration.UseTransport(new AzureServiceBusTransport(connectionString, topology)) instead.
    /// </summary>
    /// <param name="endpointConfiguration">this endpoint configuration.</param>
    /// <param name="connectionString">Connection string to use when connecting to Azure Service Bus.</param>
    /// <param name="topology">Topology to use when publishing and subscribing events.</param>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
        Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
        ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
    public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration, string connectionString, TopicTopology topology)
        where TTransport : AzureServiceBusTransport
    {
        var transport = new AzureServiceBusTransport(connectionString, topology);

        var routing = endpointConfiguration.UseTransport(transport);

        return new TransportExtensions<AzureServiceBusTransport>(transport, routing);
    }

    /// <summary>
    /// Configure the endpoint to use the Azure Service bus transport. This configuration method will eventually be deprecated.
    /// Consider using endpointConfiguration.UseTransport(new AzureServiceBusTransport(connectionString, topology)) instead.
    /// </summary>
    /// <param name="endpointConfiguration">This endpoint configuration.</param>
    /// <param name="fullyQualifiedNamespace">Fully-qualified name of Azure Service Bus namespace.</param>
    /// <param name="tokenCredential">Credentials to use when connecting to Azure Service Bus.</param>
    /// <param name="topology">Topology to use when publishing and subscribing events.</param>
    [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
        Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.",
        ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
    public static TransportExtensions<AzureServiceBusTransport> UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration, string fullyQualifiedNamespace, TokenCredential tokenCredential, TopicTopology topology)
        where TTransport : AzureServiceBusTransport
    {
        var transport = new AzureServiceBusTransport(fullyQualifiedNamespace, tokenCredential, topology);

        var routing = endpointConfiguration.UseTransport(transport);

        return new TransportExtensions<AzureServiceBusTransport>(transport, routing);
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
    public static TransportExtensions<AzureServiceBusTransport> UseWebSockets(this TransportExtensions<AzureServiceBusTransport> transportExtensions, IWebProxy? webProxy = default)
    {
        transportExtensions.Transport.UseWebSockets = true;
        if (webProxy is not null)
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
        transportExtensions.Transport.RetryPolicyOptions = retryPolicy;
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
        transportExtensions.Transport.MaxAutoLockRenewalDuration = maximumAutoLockRenewalDuration;
        return transportExtensions;
    }
}