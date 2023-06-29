namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        const int MaxMessageThresholdForTransaction = 100;

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

            AssertBelowMaxMessageThresholdForTransaction(unicastTransportOperations, multicastTransportOperations, committableTransaction);

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

        /// <summary>
        /// Throws an exception if attempting to send more than 100 messages in a transaction.
        /// This prevents the transport from experiencing this issue https://github.com/Azure/azure-sdk-for-net/issues/37265
        /// </summary>
        static void AssertBelowMaxMessageThresholdForTransaction(List<UnicastTransportOperation> unicastTransportOperations, List<MulticastTransportOperation> multicastTransportOperations, Transaction transaction)
        {
            var totalNumberOfOperations = unicastTransportOperations.Count + multicastTransportOperations.Count;
            if (transaction == null || totalNumberOfOperations <= MaxMessageThresholdForTransaction)
            {
                return;
            }

            var numberOfTransactionalOperations = 0;
            foreach (var transportOperation in unicastTransportOperations)
            {
                if (transportOperation.RequiredDispatchConsistency == DispatchConsistency.Default)
                {
                    numberOfTransactionalOperations++;
                }
            }
            foreach (var transportOperation in multicastTransportOperations)
            {
                if (transportOperation.RequiredDispatchConsistency == DispatchConsistency.Default)
                {
                    numberOfTransactionalOperations++;
                }
            }

            if (numberOfTransactionalOperations > MaxMessageThresholdForTransaction)
            {
                throw new Exception($"The number of outgoing messages ({numberOfTransactionalOperations}) exceeds the limits permitted by Azure Service Bus ({MaxMessageThresholdForTransaction}) in a single transaction");
            }
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