namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;
    using Extensibility;

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

            foreach (var transportOperation in outgoingMessages.UnicastTransportOperations)
            {
                var sender = messageSenderPool.GetMessageSender(transportOperation.Destination);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints);

                    await sender.SendAsync(message).ConfigureAwait(false);
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }

            {
                var sender = messageSenderPool.GetMessageSender("topic-1");

                try
                {
                    foreach (var transportOperation in outgoingMessages.MulticastTransportOperations)
                    {
                        var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints);

                        await sender.SendAsync(message).ConfigureAwait(false);
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }
        }
    }
}