#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    /// <summary>
    /// The Azure Service Bus transaction encapsulates the logic of sharing and accessing the transaction means
    /// such as the <see cref="ServiceBusClient"/>, the <see cref="IncomingQueuePartitionKey"/> and the <see cref="CommittableTransaction"/>
    /// between the incoming message (if available) and the outgoing messages dispatched. The logic is written in a
    /// way so that existing use cases like Azure Functions integration that do not have access to this class do not
    /// break. This is also the reason this class does some convoluted <see cref="TransportTransaction"/> access
    /// within the property accessors.
    /// </summary>
    sealed class AzureServiceBusTransaction : IDisposable
    {
        // TODO: We need to support a mode that automatically uses the ambient committable transaction
        public AzureServiceBusTransaction(bool useCrossEntityTransactions)
        {
            this.useCrossEntityTransactions = useCrossEntityTransactions;
            TransportTransaction = new TransportTransaction();
            TransportTransaction.Set(this);
        }

        public TransportTransaction TransportTransaction { get; }

        /// <summary>
        /// Gets the <see cref="CommittableTransaction"/> in case cross entity transactions are required.
        /// </summary>
        /// <returns>A committable transaction or null.</returns>
        /// <remarks>The committable transaction is lazy initialized as late as possible to make sure
        /// the transaction timeout is only started when the transaction is really needed.</remarks>
        public CommittableTransaction? CommittableTransaction
        {
            get
            {
                if (transactionIsInitialized)
                {
                    return transaction;
                }

                if (!useCrossEntityTransactions)
                {
                    transaction = default;
                }
                else
                {
                    transaction = new CommittableTransaction(new TransactionOptions
                    {
                        IsolationLevel = IsolationLevel.Serializable,
                        Timeout = TransactionManager.MaximumTimeout
                    });
                }
                transactionIsInitialized = true;

                return transaction;
            }
        }

        public ServiceBusClient? ServiceBusClient
        {
            get
            {
                if (serviceBusClientIsInitialized)
                {
                    return serviceBusClient;
                }

                if (!useCrossEntityTransactions)
                {
                    serviceBusClient = default;
                }
                else
                {
                    TransportTransaction.TryGet(out serviceBusClient);
                }
                serviceBusClientIsInitialized = true;

                return serviceBusClient;
            }
            set
            {
                if (!useCrossEntityTransactions)
                {
                    return;
                }

                TransportTransaction.Set(value);
                serviceBusClient = value;
            }
        }

        public string? IncomingQueuePartitionKey
        {
            get
            {
                if (incomingQueuePartitionKeyIsInitialized)
                {
                    return incomingQueuePartitionKey;
                }

                if (!useCrossEntityTransactions)
                {
                    incomingQueuePartitionKey = default;
                }
                else
                {
                    TransportTransaction.TryGet("IncomingQueue.PartitionKey", out incomingQueuePartitionKey);
                }
                incomingQueuePartitionKeyIsInitialized = true;

                return incomingQueuePartitionKey;
            }
            set
            {
                if (!useCrossEntityTransactions)
                {
                    return;
                }

                TransportTransaction.Set("IncomingQueue.PartitionKey", value);
                incomingQueuePartitionKey = value;
            }
        }

        public void Commit() => transaction?.Commit();

        public void Dispose() => transaction?.Dispose();

        readonly bool useCrossEntityTransactions;
        CommittableTransaction? transaction;
        bool transactionIsInitialized;
        ServiceBusClient? serviceBusClient;
        bool serviceBusClientIsInitialized;
        string? incomingQueuePartitionKey;
        bool incomingQueuePartitionKeyIsInitialized;
    }
}