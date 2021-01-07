namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
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
        public AzureServiceBusTransport(string connectionString) : base(TransportTransactionMode.SendsAtomicWithReceive)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            ConnectionString = connectionString;
        }

        /// <inheritdoc />
        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var infrastructure = new AzureServiceBusTransportInfrastructure(this);
            return Task.FromResult<TransportInfrastructure>(infrastructure);
        }

        /// <inheritdoc />
        public override string ToTransportAddress(QueueAddress address)
        {
            throw new System.NotImplementedException();
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

        /// <inheritdoc />
        public override bool SupportsDelayedDelivery { get; } = true;

        /// <inheritdoc />
        public override bool SupportsPublishSubscribe { get; } = true;

        /// <inheritdoc />
        public override bool SupportsTTBR { get; } = true;

        /// <summary>
        /// The topic name used to publish events between endpoints.
        /// </summary>
        public string TopicName { get; set; } = "bundle-1";

        /// <summary>
        /// Overrides the default maximum size used when creating queues and topics.
        /// </summary>
        public int? EntityMaximumSize { get; set; }

        /// <summary>
        /// Enables entity partitioning when creating queues and topics.
        /// </summary>
        public bool EnablePartitioning { get; set; }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count.
        /// </summary>
        public int PrefetchMultiplier { get; set; } = 10;

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value. 
        /// </summary>
        public int? PrefetchCount { get; set; }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure when the message pump cannot successfully receive a message.
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Specifies a callback to customize subscription names.
        /// </summary>
        public Func<string, string> SubscriptionNamingConvention { get; set; }

        /// <summary>
        /// Specifies a callback to customize subscription rule names.
        /// </summary>
        public Func<Type, string> SubscriptionRuleNamingConvention { get; set; }

        /// <summary>
        /// Configures the transport to use AMQP over WebSockets.
        /// </summary>
        public bool UseWebSockets { get; set; }

        /// <summary>
        /// Overrides the default token provider with a custom implementation.
        /// </summary>
        public ITokenProvider CustomTokenProvider { get; set; }

        /// <summary>
        /// Overrides the default retry policy with a custom implementation.
        /// </summary>
        public RetryPolicy CustomRetryPolicy { get; set; }

        internal string ConnectionString { get; private set; }
    }
}