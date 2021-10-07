namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

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
            transaction.TryGet<ServiceBusClient>(out var serviceBusClient);
            transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);
            transaction.TryGet<CommittableTransaction>(out var committableTransaction);

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

                var sender = messageSenderPool.GetMessageSender(destination, serviceBusClient);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                    var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendMessageAsync(message));

                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, serviceBusClient);
                }
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var sender = messageSenderPool.GetMessageSender(topicName, serviceBusClient);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                    var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendMessageAsync(message));
                        //committable tx will not be committed because this scope is not the owner
                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, serviceBusClient);
                }
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        static void ApplyCustomizationToOutgoingNativeMessage(ReadOnlyContextBag context, IOutgoingTransportOperation transportOperation, ServiceBusMessage message)
        {
            if (transportOperation.Message.Headers.TryGetValue(CustomizeNativeMessageExtensions.CustomizationHeader, out var customizationId))
            {
                if (context.TryGet<NativeMessageCustomizer>(out var nmc) && nmc.Customizations.Keys.Contains(customizationId))
                {
                    nmc.Customizations[customizationId].Invoke(message);
                }

                transportOperation.Message.Headers.Remove(CustomizeNativeMessageExtensions.CustomizationHeader);
            }
        }
    }
}