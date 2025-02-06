namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Transport;
using Transport.AzureServiceBus;

/// <summary>
/// Transport definition for Azure Service Bus.
/// </summary>
public partial class AzureServiceBusTransport : TransportDefinition
{
    /// <summary>
    /// Creates a new instance of <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    /// <param name="connectionString">Connection string to use when connecting to Azure Service Bus.</param>
    /// <param name="topology">Topology to use when publishing and subscribing events.</param>
    public AzureServiceBusTransport(string connectionString, TopicTopology topology) : base(
        defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
        supportsDelayedDelivery: true,
        supportsPublishSubscribe: true,
        supportsTTBR: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(topology);

        ConnectionString = connectionString;
        Topology = topology;
    }

    /// <summary>
    /// Creates a new instance of <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    /// <param name="fullyQualifiedNamespace">Fully-qualified name of Azure Service Bus namespace.</param>
    /// <param name="tokenCredential">Credentials to use when connecting to Azure Service Bus.</param>
    /// <param name="topology">Topology to use when publishing and subscribing events.</param>
    public AzureServiceBusTransport(string fullyQualifiedNamespace, TokenCredential tokenCredential, TopicTopology topology) : base(
        defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
        supportsDelayedDelivery: true,
        supportsPublishSubscribe: true,
        supportsTTBR: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
        ArgumentNullException.ThrowIfNull(tokenCredential);
        ArgumentNullException.ThrowIfNull(topology);

        FullyQualifiedNamespace = fullyQualifiedNamespace;
        TokenCredential = tokenCredential;
        Topology = topology;
    }

    /// <inheritdoc />
    public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings,
        ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
    {
        // This check prevents an uninitialized transport template to be used.
        if (ConnectionString == null && FullyQualifiedNamespace == null)
        {
            throw new Exception("The transport has not been initialized. Either provide a connection string or a fully qualified namespace and token credential.");
        }

        Topology.Validate();

        var transportType = UseWebSockets ? ServiceBusTransportType.AmqpWebSockets : ServiceBusTransportType.AmqpTcp;
        bool enableCrossEntityTransactions = TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive;

        var receiveSettingsAndClientPairs = receivers.Select(receiver =>
        {
            var receiveClientOptions = new ServiceBusClientOptions
            {
                TransportType = transportType,
                EnableCrossEntityTransactions = enableCrossEntityTransactions,
                Identifier = $"Client-{receiver.Id}-{receiver.ReceiveAddress}-{Guid.NewGuid()}",
            };
            ApplyRetryPolicyOptionsIfNeeded(receiveClientOptions);
            ApplyWebProxyIfNeeded(receiveClientOptions);
            var receiveClient = TokenCredential != null
                ? new ServiceBusClient(FullyQualifiedNamespace, TokenCredential, receiveClientOptions)
                : new ServiceBusClient(ConnectionString, receiveClientOptions);
            return (receiver, receiveClient);
        }).ToArray();

        var defaultClientOptions = new ServiceBusClientOptions
        {
            TransportType = transportType,
            // for the default client we never want things to automatically use cross entity transaction
            EnableCrossEntityTransactions = false,
            Identifier = $"Client-{hostSettings.Name}-{Guid.NewGuid()}"
        };
        ApplyRetryPolicyOptionsIfNeeded(defaultClientOptions);
        ApplyWebProxyIfNeeded(defaultClientOptions);
        var defaultClient = TokenCredential != null
            ? new ServiceBusClient(FullyQualifiedNamespace, TokenCredential, defaultClientOptions)
            : new ServiceBusClient(ConnectionString, defaultClientOptions);

        var administrationClient = TokenCredential != null
            ? new ServiceBusAdministrationClient(FullyQualifiedNamespace, TokenCredential)
            : new ServiceBusAdministrationClient(ConnectionString);

        var infrastructure = new AzureServiceBusTransportInfrastructure(this, hostSettings, receiveSettingsAndClientPairs, defaultClient, administrationClient);

        if (hostSettings.SetupInfrastructure)
        {
            await administrationClient.AssertNamespaceManageRightsAvailable(cancellationToken)
                .ConfigureAwait(false);

            var allQueues = infrastructure.Receivers
                .Select(r => r.Value.ReceiveAddress)
                .Concat(sendingAddresses)
                .ToArray();

            var queueCreator = new TopologyCreator(this);
            await queueCreator.Create(administrationClient, allQueues, cancellationToken).ConfigureAwait(false);

            foreach (IMessageReceiver messageReceiver in infrastructure.Receivers.Values)
            {
                if (messageReceiver.Subscriptions is SubscriptionManager subscriptionManager)
                {
                    await subscriptionManager.SetupInfrastructureIfNecessary(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return infrastructure;
        }

        return infrastructure;

    }

    void ApplyRetryPolicyOptionsIfNeeded(ServiceBusClientOptions options)
    {
        if (RetryPolicyOptions != null)
        {
            options.RetryOptions = RetryPolicyOptions;
        }
    }

    void ApplyWebProxyIfNeeded(ServiceBusClientOptions options)
    {
        if (WebProxy != null)
        {
            options.WebProxy = WebProxy;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() =>
    [
        TransportTransactionMode.None,
        TransportTransactionMode.ReceiveOnly,
        TransportTransactionMode.SendsAtomicWithReceive
    ];

    /// <summary>
    /// Gets the topic topology used.
    /// </summary>
    [MemberNotNull(nameof(topology))]
    public TopicTopology Topology
    {
        get
        {
            ArgumentNullException.ThrowIfNull(topology);
            return topology;
        }
        internal set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value is not (MigrationTopology or TopicPerEventTopology))
            {
                throw new ArgumentException("The provided topology is not supported.", nameof(value));
            }
            topology = value;
        }
    }

    TopicTopology topology;

    /// <summary>
    /// The maximum size used when creating queues and topics in GB.
    /// </summary>
    public int EntityMaximumSize
    {
        get => entityMaximumSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(EntityMaximumSize));
            entityMaximumSize = value;
        }
    }
    int entityMaximumSize = 5;

    internal int EntityMaximumSizeInMegabytes => EntityMaximumSize * 1024;

    /// <summary>
    /// Enables entity partitioning when creating queues and topics.
    /// </summary>
    public bool EnablePartitioning { get; set; }

    /// <summary>
    /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
    /// </summary>
    public int PrefetchMultiplier
    {
        get => prefetchMultiplier;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(PrefetchMultiplier));
            prefetchMultiplier = value;
        }
    }
    int prefetchMultiplier = 10;

    /// <summary>
    /// Overrides the default prefetch count calculation with the specified value.
    /// </summary>
    public int? PrefetchCount
    {
        get => prefetchCount;
        set
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Value, nameof(PrefetchCount));
            }

            prefetchCount = value;
        }

    }
    int? prefetchCount;

    /// <summary>
    /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump cannot successfully receive a message.
    /// </summary>
    public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker
    {
        get => timeToWaitBeforeTriggeringCircuitBreaker;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Ticks, nameof(TimeToWaitBeforeTriggeringCircuitBreaker));
            timeToWaitBeforeTriggeringCircuitBreaker = value;
        }
    }
    TimeSpan timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the maximum duration within which the lock will be renewed automatically. This
    /// value should be greater than the longest message lock duration.
    /// </summary>
    /// <value>The maximum duration during which message locks are automatically renewed. The default value is 5 minutes.</value>
    /// <remarks>The message renew can continue for sometime in the background
    /// after completion of message and result in a few false MessageLockLostExceptions temporarily.</remarks>
    public TimeSpan? MaxAutoLockRenewalDuration
    {
        get => maxAutoLockRenewalDuration;
        set
        {
            if (value.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Value.Ticks, nameof(MaxAutoLockRenewalDuration));
            }

            maxAutoLockRenewalDuration = value;
        }
    }
    TimeSpan? maxAutoLockRenewalDuration;

    /// <summary>
    /// Configures the transport to use AMQP over WebSockets.
    /// </summary>
    public bool UseWebSockets { get; set; }

    /// <summary>
    /// Overrides the default retry policy.
    /// </summary>
    public ServiceBusRetryOptions? RetryPolicyOptions
    {
        get => retryPolicy;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RetryPolicyOptions));
            retryPolicy = value;
        }
    }
    ServiceBusRetryOptions? retryPolicy;

    /// <summary>
    /// The proxy to use for communication over web sockets.
    /// </summary>
    public IWebProxy? WebProxy
    {
        get => webProxy;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(WebProxy));
            webProxy = value;
        }
    }
    IWebProxy? webProxy;

    /// <summary>
    /// When set will not add `NServiceBus.Transport.Encoding` header for wire compatibility with NServiceBus.AzureServiceBus. The default value is <c>false</c>.
    /// </summary>
    [ObsoleteEx(Message = "Next versions of the transport will by default no longer send the transport encoding header for wire compatibility, requiring an opt-in for the header to be sent.",
        TreatAsErrorFromVersion = "5",
        RemoveInVersion = "6")]
    public bool DoNotSendTransportEncodingHeader { get; set; }

    /// <summary>
    /// Gets or sets the action that allows customization of the native <see cref="ServiceBusMessage"/> 
    /// just before it is dispatched to the Azure Service Bus SDK client. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// This customization is applied after any configured transport customizations, meaning that 
    /// any changes made here may override or conflict with previous transport-level adjustments. 
    /// Exercise caution, as modifying the message at this stage can lead to unintended behavior 
    /// downstream if the message structure or properties are altered in ways that do not align 
    /// with expectations elsewhere in the system.
    /// </para>
    /// </remarks>
    public OutgoingNativeMessageCustomizationAction? OutgoingNativeMessageCustomization { get; set; }

    internal string? ConnectionString { get; set; }

    internal string? FullyQualifiedNamespace { get; set; }
    internal TokenCredential? TokenCredential { get; set; }
}