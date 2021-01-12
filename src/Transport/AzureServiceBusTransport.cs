namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
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
        public AzureServiceBusTransport(string connectionString) : base(TransportTransactionMode.SendsAtomicWithReceive, true, true, true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            ConnectionString = connectionString;
        }

        /// <inheritdoc />
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var infrastructure = new AzureServiceBusTransportInfrastructure(this, hostSettings);

            await infrastructure.Initialize(receivers, sendingAddresses).ConfigureAwait(false);
            
            return infrastructure;
        }

        /// <inheritdoc />
        public override string ToTransportAddress(QueueAddress address)
        {
            //TODO is it really worthwhile using a StringBuilder here?
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append($"-{address.BaseAddress}");
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
        public string TopicName { get; set; } = "bundle-1";

        /// <summary>
        /// The maximum size used when creating queues and topics in GB.
        /// </summary>
        public int EntityMaximumSize { get; set; } = 5;

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
        public Func<string, string> SubscriptionNamingConvention { get; set; } = name => name;

        /// <summary>
        /// Specifies a callback to customize subscription rule names.
        /// </summary>
        public Func<Type, string> SubscriptionRuleNamingConvention { get; set; } = type => type.FullName;

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