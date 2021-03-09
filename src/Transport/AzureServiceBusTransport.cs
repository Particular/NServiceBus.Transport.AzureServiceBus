namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Transport;
    using Transport.AzureServiceBus;

    /// <summary>Transport definition for Azure Service Bus.</summary>
    public class AzureServiceBusTransport : TransportDefinition
    {
        /// <summary>
        /// Creates a new instance of <see cref="AzureServiceBusTransport"/>.
        /// </summary>
        public AzureServiceBusTransport(string connectionString) : this()
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            ConnectionString = connectionString;
        }

        // Used by the shim API to enable easier migration from the existing API
        internal AzureServiceBusTransport() : base(
            defaultTransactionMode: TransportTransactionMode.SendsAtomicWithReceive,
            supportsDelayedDelivery: true,
            supportsPublishSubscribe: true,
            supportsTTBR: true)
        {
        }

        /// <inheritdoc />
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings,
            ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken)
        {
            // Check if a connection string has been provided when using the shim configuration API.
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception(
                    "No transport connection string has been configured via the 'ConnectionString' method. Provide a connection string using 'endpointConfig.UseTransport<AzureServiceBusTransport>().ConnectionString(connectionString)'.");
            }

            var connectionStringBuilder = new ServiceBusConnectionStringBuilder(ConnectionString)
            {
                TransportType = UseWebSockets ? TransportType.AmqpWebSockets : TransportType.Amqp
            };
            var namespacePermissions = new NamespacePermissions(connectionStringBuilder, TokenProvider);

            var infrastructure = new AzureServiceBusTransportInfrastructure(this, hostSettings, receivers, connectionStringBuilder, namespacePermissions);

            if (hostSettings.SetupInfrastructure)
            {
                var queueCreator = new QueueCreator(this, connectionStringBuilder, namespacePermissions);
                var allQueues = receivers
                    .Select(r => r.ReceiveAddress)
                    .Concat(sendingAddresses)
                    .ToArray();

                await queueCreator.CreateQueues(allQueues).ConfigureAwait(false);
            }

            return infrastructure;
        }

        /// <inheritdoc />
        public override string ToTransportAddress(QueueAddress address)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append($"-{address.Discriminator}");
            }

            if (address.Qualifier != null)
            {
                queue.Append($".{address.Qualifier}");
            }

            return queue.ToString();
        }

        /// <inheritdoc />
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            return new[]
            {
                TransportTransactionMode.None,
                TransportTransactionMode.ReceiveOnly,
                TransportTransactionMode.SendsAtomicWithReceive
            };
        }

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
        public ITokenProvider TokenProvider { get; set; }

        /// <summary>
        /// Overrides the default retry policy.
        /// </summary>
        public RetryPolicy RetryPolicy
        {
            get => retryPolicy;
            set
            {
                Guard.AgainstNull(nameof(RetryPolicy), value);
                retryPolicy = value;
            }
        }
        RetryPolicy retryPolicy;

        internal string ConnectionString { get; set; }
    }
}