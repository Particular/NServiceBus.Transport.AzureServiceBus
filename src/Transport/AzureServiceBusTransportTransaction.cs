#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    /// <summary>
    /// The Azure Service Bus transaction encapsulates the logic of sharing and accessing the transaction means
    /// such as the <see cref="ServiceBusClient"/>, the <see cref="IncomingQueuePartitionKey"/> and the <see cref="CommittableTransaction"/>
    /// between the incoming message (if available) and the outgoing messages dispatched.
    /// </summary>
    public sealed class AzureServiceBusTransportTransaction : IDisposable
    {
        /// <summary>
        /// Creates a new instance of an <see cref="AzureServiceBusTransportTransaction"/> with an optional transport transaction.
        /// </summary>
        /// <remarks>The current instance registers itself automatically in the transport transaction.</remarks>
        /// <param name="transportTransaction">An optional transport transaction instance.</param>
        public AzureServiceBusTransportTransaction(TransportTransaction? transportTransaction = null)
        {
            TransportTransaction = transportTransaction ?? new TransportTransaction();
            TransportTransaction.Set(this);
        }

        /// <summary>
        /// Creates a new instance of an <see cref="AzureServiceBusTransportTransaction"/> with the provided connection information.
        /// The connection information is necessary in cases cross entity transactions are in use.
        /// </summary>
        /// <remarks>The current instance registers itself automatically in the transport transaction.</remarks>
        /// <param name="serviceBusClient">The service bus client to be used for creating senders.</param>
        /// <param name="incomingQueuePartitionKey">The incoming queue partition key to be used to set <see cref="ServiceBusMessage.TransactionPartitionKey"/></param>
        /// <param name="transactionOptions">The transaction options to be used when the underlying committable transaction is created.</param>
        /// <param name="transportTransaction">An optional transport transaction instance.</param>
        public AzureServiceBusTransportTransaction(ServiceBusClient serviceBusClient, string incomingQueuePartitionKey,
            TransactionOptions transactionOptions, TransportTransaction? transportTransaction = null)
        : this(transportTransaction)
        {
            ServiceBusClient = serviceBusClient;
            IncomingQueuePartitionKey = incomingQueuePartitionKey;
            this.transactionOptions = transactionOptions;
        }

        /// <summary>
        /// Gets the currently owned transport transaction.
        /// </summary>
        public TransportTransaction TransportTransaction { get; }

        /// <summary>
        /// Gets the <see cref="CommittableTransaction"/> in case cross entity transactions are used.
        /// </summary>
        /// <returns>A committable transaction or null.</returns>
        /// <remarks>The committable transaction is lazy initialized as late as possible to make sure
        /// the transaction timeout is only started when the transaction is really needed.</remarks>
        internal CommittableTransaction? CommittableTransaction
        {
            get
            {
                if (transactionIsInitialized)
                {
                    return transaction;
                }

                transaction = !transactionOptions.HasValue ? default : new CommittableTransaction(transactionOptions.Value);
                transactionIsInitialized = true;
                return transaction;
            }
        }

        internal ServiceBusClient? ServiceBusClient
        {
            get;
        }

        internal string? IncomingQueuePartitionKey
        {
            get;
        }

        /// <summary>
        /// Commits the underlying committable transaction in case one was created.
        /// </summary>
        public void Commit() => transaction?.Commit();

        /// <summary>
        /// Disposes the underlying committable transaction in case one was created.
        /// </summary>
        public void Dispose() => transaction?.Dispose();

        readonly TransactionOptions? transactionOptions;
        CommittableTransaction? transaction;
        bool transactionIsInitialized;
    }
}