namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
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
        public AzureServiceBusTransport(string connectionString) : base(
            defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            supportsDelayedDelivery: true,
            supportsPublishSubscribe: true,
            supportsTTBR: true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            ConnectionString = connectionString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="AzureServiceBusTransport"/>.
        /// </summary>
        public AzureServiceBusTransport(string fullyQualifiedNamespace, TokenCredential tokenCredential) : base(
            defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            supportsDelayedDelivery: true,
            supportsPublishSubscribe: true,
            supportsTTBR: true)
        {
            Guard.AgainstNullAndEmpty(nameof(fullyQualifiedNamespace), fullyQualifiedNamespace);
            Guard.AgainstNull(nameof(tokenCredential), tokenCredential);

            FullyQualifiedNamespace = fullyQualifiedNamespace;
            TokenCredential = tokenCredential;
        }

        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811", Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        internal AzureServiceBusTransport() : base(
            defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            supportsDelayedDelivery: true,
            supportsPublishSubscribe: true,
            supportsTTBR: true)
        {
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

            var infrastructure = new AzureServiceBusTransportInfrastructure(this, hostSettings, receiveSettingsAndClientPairs, defaultClient);

            if (hostSettings.SetupInfrastructure)
            {
                var namespacePermissions = new NamespacePermissions(TokenCredential, FullyQualifiedNamespace, ConnectionString);
                var adminClient = await namespacePermissions.CanManage(cancellationToken)
                    .ConfigureAwait(false);

                var queueCreator = new QueueCreator(this);
                var allQueues = infrastructure.Receivers
                    .Select(r => r.Value.ReceiveAddress)
                    .Concat(sendingAddresses)
                    .ToArray();

                await queueCreator.CreateQueues(adminClient, allQueues, cancellationToken).ConfigureAwait(false);

                foreach (IMessageReceiver messageReceiver in infrastructure.Receivers.Values)
                {
                    if (messageReceiver.Subscriptions is SubscriptionManager subscriptionManager)
                    {
                        await subscriptionManager.CreateSubscription(adminClient, cancellationToken).ConfigureAwait(false);
                    }
                }
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
            new[]
            {
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive
            };

        /// <summary>
        /// Gets or sets the topic topology to be used.
        /// </summary>
        /// <remarks>The default is <see cref="TopicTopology.DefaultBundle"/></remarks>
        public TopicTopology Topology { get; set; } = TopicTopology.DefaultBundle;

        /// <summary>
        /// The maximum size used when creating queues and topics in GB.
        /// </summary>
        public int EntityMaximumSize
        {
            get => entityMaximumSize;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(EntityMaximumSize), value);
                entityMaximumSize = value;
            }
        }
        int entityMaximumSize = 5;

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
                Guard.AgainstNegativeAndZero(nameof(PrefetchMultiplier), value);
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
                    Guard.AgainstNegative(nameof(PrefetchCount), value.Value);
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
                Guard.AgainstNegativeAndZero(nameof(TimeToWaitBeforeTriggeringCircuitBreaker), value);
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
                    Guard.AgainstNegative(nameof(MaxAutoLockRenewalDuration), value.Value);
                    maxAutoLockRenewalDuration = value;
                }
            }
        }
        TimeSpan? maxAutoLockRenewalDuration;

        /// <summary>
        /// Specifies a callback to customize subscription names.
        /// </summary>
        public Func<string, string> SubscriptionNamingConvention
        {
            get => subscriptionNamingConvention;
            set
            {
                Guard.AgainstNull(nameof(SubscriptionNamingConvention), value);

                // wrap the custom convention:
                subscriptionNamingConvention = subscriptionName =>
                {
                    try
                    {
                        return value(subscriptionName);
                    }
                    catch (Exception exception)
                    {
                        throw new Exception("Custom subscription naming convention threw an exception.", exception);
                    }
                };

            }
        }
        Func<string, string> subscriptionNamingConvention = static name => name;

        /// <summary>
        /// Specifies a callback to customize subscription rule names.
        /// </summary>
        public Func<Type, string> SubscriptionRuleNamingConvention
        {
            get => subscriptionRuleNamingConvention;
            set
            {
                Guard.AgainstNull(nameof(SubscriptionRuleNamingConvention), value);

                // wrap the custom convention:
                subscriptionRuleNamingConvention = eventType =>
                {
                    try
                    {
                        return value(eventType);
                    }
                    catch (Exception exception)
                    {
                        throw new Exception("Custom subscription rule naming convention threw an exception", exception);
                    }
                };
            }
        }
        Func<Type, string> subscriptionRuleNamingConvention = static type => type.FullName;

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        public bool UseWebSockets { get; set; }

        /// <summary>
        /// Overrides the default retry policy.
        /// </summary>
        public ServiceBusRetryOptions RetryPolicyOptions
        {
            get => retryPolicy;
            set
            {
                Guard.AgainstNull(nameof(RetryPolicyOptions), value);
                retryPolicy = value;
            }
        }
        ServiceBusRetryOptions retryPolicy;

        /// <summary>
        /// The proxy to use for communication over web sockets.
        /// </summary>
        public IWebProxy WebProxy
        {
            get => webProxy;
            set
            {
                Guard.AgainstNull(nameof(WebProxy), value);
                webProxy = value;
            }
        }
        IWebProxy webProxy;

        internal string ConnectionString { get; set; }

        internal string FullyQualifiedNamespace { get; set; }
        internal TokenCredential TokenCredential { get; set; }

        /// <summary>
        /// Disable legacy headers for wire compatibility with NServiceBus.AzureServiceBus
        /// </summary>
        public void DisableLegacyTransportCompatibility()
        {
            disableLegacyTransportCompatibility = true;
        }
        internal bool disableLegacyTransportCompatibility;
    }
}