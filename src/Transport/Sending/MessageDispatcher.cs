namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;

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

            transaction.TryGet<(ServiceBusConnection, string)>(out var receiverConnectionAndPath);
            transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);

            var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
            var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

            var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

            foreach (var transportOperation in unicastTransportOperations)
            {
                var receiverConnectionAndPathToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? (null, null) : receiverConnectionAndPath;
                var destination = transportOperation.Destination;

                tasks.Add(Send(destination, receiverConnectionAndPathToUse, transportOperation, partitionKey));
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var receiverConnectionAndPathToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? (null, null) : receiverConnectionAndPath;

                tasks.Add(Send(topicName, receiverConnectionAndPathToUse, transportOperation, partitionKey));
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        async Task Send(string destination, (ServiceBusConnection, string) receiverConnectionAndPathToUse, IOutgoingTransportOperation transportOperation, string partitionKey)
        {
            MessageSender sender = null;

            try
            {
                sender = messageSenderPool.GetMessageSender(destination, receiverConnectionAndPathToUse);

                var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                using (var scope = CreateTransactionScope(transportOperation.RequiredDispatchConsistency))
                {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    scope?.Complete();
                }
            }
            finally
            {
                if (sender != null)
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }
        }

        TransactionScope CreateTransactionScope(DispatchConsistency dispatchConsistency)
        {
            return dispatchConsistency == DispatchConsistency.Isolated
                ? new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled)
                : null;
        }
    }
}