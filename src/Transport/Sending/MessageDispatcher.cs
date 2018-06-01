namespace NServiceBus.Transport.AzureServiceBus
{
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

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            // Assumption: we're not implementing batching as it will be done by ASB client

            transaction.TryGet<ServiceBusConnection>(out var connection);
            transaction.TryGet<string>("IncomingQueue", out var incomingQueue);
            transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);

            foreach (var transportOperation in outgoingMessages.UnicastTransportOperations)
            {
                var connectionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : connection;

                var sender = messageSenderPool.GetMessageSender(transportOperation.Destination, connectionToUse, incomingQueue);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    await sender.SendAsync(message).ConfigureAwait(false);
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, connectionToUse);
                }
            }

            foreach (var transportOperation in outgoingMessages.MulticastTransportOperations)
            {
                var connectionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : connection;

                var sender = messageSenderPool.GetMessageSender("topic-1", connectionToUse, incomingQueue);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    await sender.SendAsync(message).ConfigureAwait(false);
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, connectionToUse);
                }
            }
        }
    }
}