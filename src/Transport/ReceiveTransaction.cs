#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    sealed class ReceiveTransaction : IDisposable
    {
        public ReceiveTransaction(bool useTransactions)
        {
            this.useTransactions = useTransactions;
            TransportTransaction = new TransportTransaction();
            TransportTransaction.Set(this);
        }

        public TransportTransaction TransportTransaction { get; }

        public CommittableTransaction? CommittableTransaction
        {
            get
            {
                if (useTransactions && transaction == null)
                {
                    transaction = new CommittableTransaction(new TransactionOptions
                    {
                        IsolationLevel = IsolationLevel.Serializable,
                        Timeout = TransactionManager.MaximumTimeout
                    });
                }
                return transaction;
            }
        }

        public ServiceBusClient? ServiceBusClient
        {
            get
            {
                TransportTransaction.TryGet(out ServiceBusClient serviceBusClient);
                return serviceBusClient;
            }
            set
            {
                if (useTransactions)
                {
                    TransportTransaction.Set(value);
                }
            }
        }

        public string? IncomingQueuePartitionKey
        {
            get
            {
                TransportTransaction.TryGet("IncomingQueue.PartitionKey", out string serviceBusClient);
                return serviceBusClient;
            }
            set
            {
                if (useTransactions)
                {
                    TransportTransaction.Set("IncomingQueue.PartitionKey", value);
                }
            }
        }

        public void Commit() => transaction?.Commit();

        public void Dispose() => transaction?.Dispose();

        readonly bool useTransactions;
        CommittableTransaction? transaction;
    }
}