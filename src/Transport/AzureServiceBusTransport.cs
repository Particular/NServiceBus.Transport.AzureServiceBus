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
    using Azure.Messaging.ServiceBus.Administration;
    using Transport;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Transport definition for Azure Service Bus.
    /// </summary>
    public class AzureServiceBusTransport : TransportDefinition
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

        [PreObsolete(RemoveInVersion = "4", Note = "Will not be required by TransportExtensions methods anymore in 4.0")]
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
            var transportType = UseWebSockets ? ServiceBusTransportType.AmqpWebSockets : ServiceBusTransportType.AmqpTcp;
            bool enableCrossEntityTransactions = TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive;

            var receiveSettingsAndClientPairs = receivers.Select(receiver =>
            {
                var options = new ServiceBusClientOptions
                {
                    TransportType = transportType,
                    EnableCrossEntityTransactions = enableCrossEntityTransactions,
                    Identifier = $"Client-{receiver.Id}-{receiver.ReceiveAddress}-{Guid.NewGuid()}",
                };
                ApplyRetryPolicyOptionsIfNeeded(options);
                ApplyWebProxyIfNeeded(options);
                var client = TokenCredential != null
                    ? new ServiceBusClient(ConnectionString, TokenCredential, options)
                    : new ServiceBusClient(ConnectionString, options);
                return (receiver, client);
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
                ? new ServiceBusClient(ConnectionString, TokenCredential, defaultClientOptions)
                : new ServiceBusClient(ConnectionString, defaultClientOptions);

            var administrativeClient = TokenCredential != null ? new ServiceBusAdministrationClient(ConnectionString, TokenCredential) : new ServiceBusAdministrationClient(ConnectionString);

            var namespacePermissions = new NamespacePermissions(administrativeClient);

            var infrastructure = new AzureServiceBusTransportInfrastructure(this, hostSettings, receiveSettingsAndClientPairs, defaultClient, administrativeClient, namespacePermissions);

            if (hostSettings.SetupInfrastructure)
            {
                var queueCreator = new QueueCreator(this, administrativeClient, namespacePermissions);
                var allQueues = infrastructure.Receivers
                    .Select(r => r.Value.ReceiveAddress)
                    .Concat(sendingAddresses)
                    .ToArray();

                await queueCreator.CreateQueues(allQueues, cancellationToken).ConfigureAwait(false);
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
        [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
                    TreatAsErrorFromVersion = "4",
                    RemoveInVersion = "5")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address) => AzureServiceBusTransportInfrastructure.TranslateAddress(address);
#pragma warning restore CS0672 // Member overrides obsolete member

        /// <inheritdoc />
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() =>
            new[]
            {
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive
            };

        /// <summary>
        /// The topic name used to publish events between endpoints.
        /// </summary>
        public string TopicName
        {
            get => topicName;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(TopicName), value);
                topicName = value;
            }
        }
        string topicName = "bundle-1";

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
        Func<string, string> subscriptionNamingConvention = name => name;

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
        Func<Type, string> subscriptionRuleNamingConvention = type => type.FullName;

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        public bool UseWebSockets { get; set; }

        /// <summary>
        /// Overrides the default token provider.
        /// </summary>
        public TokenCredential TokenCredential { get; set; }

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

        /// <summary>
        /// Configures the Service Bus connection string.
        /// </summary>
        protected internal string ConnectionString { get; set; }
    }
}