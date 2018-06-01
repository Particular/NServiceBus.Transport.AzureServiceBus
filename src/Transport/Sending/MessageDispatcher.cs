namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;

    class MessageDispatcher : IDispatchMessages
    {
        readonly MessageSenderPool messageSenderPool;

        public MessageDispatcher(MessageSenderPool messageSenderPool)
        {
            this.messageSenderPool = messageSenderPool;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            // Assumption: we're not implementing batching as it will be done by ASB client

            transaction.TryGet<ServiceBusConnection>(out var connection);
            transaction.TryGet<string>("IncomingQueue", out var incomingQueue);
            transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);

            var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
            var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

            var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

            foreach (var transportOperation in unicastTransportOperations)
            {
                var connectionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : connection;

                var sender = messageSenderPool.GetMessageSender(transportOperation.Destination, connectionToUse, incomingQueue);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    tasks.Add(sender.SendAsync(message));
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, connectionToUse);
                }
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var connectionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : connection;

                var sender = messageSenderPool.GetMessageSender("topic-1", connectionToUse, incomingQueue);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    tasks.Add(sender.SendAsync(message));
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, connectionToUse);
                }
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }
    }
}