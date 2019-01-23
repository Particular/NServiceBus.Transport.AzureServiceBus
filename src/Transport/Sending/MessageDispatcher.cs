namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;

    class MessageDispatcher : IDispatchMessages
    {
        readonly MessageSenderPool messageSenderPool;
        readonly string topicName;

        public MessageDispatcher(MessageSenderPool messageSenderPool, string topicName)
        {
            this.messageSenderPool = messageSenderPool;
            this.topicName = topicName;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            // Assumption: we're not implementing batching as it will be done by ASB client

            var receiverConnectionAndPathFound = transaction.TryGet<(ServiceBusConnection, string)>(out var receiverConnectionAndPath);
            var partitionKeyFound = transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);
            var shouldSuppressTransaction = !(receiverConnectionAndPathFound && partitionKeyFound);

            var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
            var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

            var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

            foreach (var transportOperation in unicastTransportOperations)
            {
                var destination = transportOperation.Destination;

                // Workaround for reply-to address set by ASB transport
                var index = transportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination.Substring(0, index);
                }

                var receiverConnectionAndPathToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? (null, null) : receiverConnectionAndPath;

                var sender = messageSenderPool.GetMessageSender(destination, receiverConnectionAndPathToUse);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    using (var scope = CreateTransactionScope(transportOperation.RequiredDispatchConsistency, shouldSuppressTransaction))
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendAsync(message));
                        scope?.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var receiverConnectionAndPathToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? (null, null) : receiverConnectionAndPath;

                var sender = messageSenderPool.GetMessageSender(topicName, receiverConnectionAndPathToUse);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    using (var scope = CreateTransactionScope(transportOperation.RequiredDispatchConsistency, shouldSuppressTransaction))
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendAsync(message));
                        scope?.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        TransactionScope CreateTransactionScope(DispatchConsistency dispatchConsistency, bool shouldSuppressTransaction)
        {
            return dispatchConsistency == DispatchConsistency.Isolated || shouldSuppressTransaction
                ? new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled)
                : null;
        }
    }
}