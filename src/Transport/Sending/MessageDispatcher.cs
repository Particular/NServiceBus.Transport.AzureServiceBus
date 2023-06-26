namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        readonly MessageSenderRegistry messageSenderRegistry;
        readonly string topicName;

        public MessageDispatcher(MessageSenderRegistry messageSenderRegistry, string topicName)
        {
            this.messageSenderRegistry = messageSenderRegistry;
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

                var sender = messageSenderRegistry.GetMessageSender(destination, serviceBusClient);

                var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                tasks.Add(DispatchOperation(sender, message, transactionToUse));
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var sender = messageSenderRegistry.GetMessageSender(topicName, serviceBusClient);

                var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                tasks.Add(DispatchOperation(sender, message, transactionToUse));
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        static async Task DispatchOperation(ServiceBusSender sender, ServiceBusMessage message, CommittableTransaction transactionToUse)
        {
            using (var scope = transactionToUse.ToScope())
            {
                await sender.SendMessageAsync(message).ConfigureAwait(false);
                scope.Complete();
            }
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