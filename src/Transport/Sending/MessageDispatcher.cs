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
        static readonly Dictionary<string, List<IOutgoingTransportOperation>> EmptyDestinationAndOperations = new Dictionary<string, List<IOutgoingTransportOperation>>();

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

            var transportOperations =
                new List<IOutgoingTransportOperation>(unicastTransportOperations.Count +
                                                      multicastTransportOperations.Count);
            transportOperations.AddRange(unicastTransportOperations);
            transportOperations.AddRange(multicastTransportOperations);

            Dictionary<string, List<IOutgoingTransportOperation>> isolatedOperationsPerDestination = null;
            Dictionary<string, List<IOutgoingTransportOperation>> defaultOperationsPerDestination = null;
            var numberOfIsolatedOperations = 0;
            var numberOfDefaultOperations = 0;

            foreach (var operation in transportOperations)
            {
                var destination = operation.ExtractDestination(defaultMulticastRoute: topicName);
                switch (operation.RequiredDispatchConsistency)
                {
                    case DispatchConsistency.Default:
                        numberOfDefaultOperations++;
                        defaultOperationsPerDestination ??=
                            new Dictionary<string, List<IOutgoingTransportOperation>>(StringComparer.OrdinalIgnoreCase);

                        if (!defaultOperationsPerDestination.ContainsKey(destination))
                        {
                            defaultOperationsPerDestination[destination] = new List<IOutgoingTransportOperation> { operation };
                            // because we batch only the number of destinations are relevant
                        }
                        else
                        {
                            defaultOperationsPerDestination[destination].Add(operation);
                        }
                        break;
                    case DispatchConsistency.Isolated:
                        // every isolated operation counts
                        numberOfIsolatedOperations++;
                        isolatedOperationsPerDestination ??=
                            new Dictionary<string, List<IOutgoingTransportOperation>>(StringComparer.OrdinalIgnoreCase);
                        if (!isolatedOperationsPerDestination.ContainsKey(destination))
                        {
                            isolatedOperationsPerDestination[destination] = new List<IOutgoingTransportOperation> { operation };
                        }
                        else
                        {
                            isolatedOperationsPerDestination[destination].Add(operation);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var concurrentDispatchTasks = new List<Task>(numberOfIsolatedOperations + numberOfDefaultOperations);
            AddOperationsTo(concurrentDispatchTasks, isolatedOperationsPerDestination ?? EmptyDestinationAndOperations, context, serviceBusClient, partitionKey, null);
            AddOperationsTo(concurrentDispatchTasks, defaultOperationsPerDestination ?? EmptyDestinationAndOperations, context, serviceBusClient, partitionKey, committableTransaction);

            return concurrentDispatchTasks.Count == 1 ? concurrentDispatchTasks[0] : Task.WhenAll(concurrentDispatchTasks);
        }

        // The parameters of this method are deliberately mutable and of the original collection type to make sure
        // no boxing occurs
        void AddOperationsTo(List<Task> dispatchTasks,
            Dictionary<string, List<IOutgoingTransportOperation>> transportOperationsPerDestination,
            ContextBag context,
            ServiceBusClient serviceBusClient,
            string partitionKey,
            CommittableTransaction transactionToUse)
        {
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var operations = destinationAndOperations.Value;

                var sender = messageSenderRegistry.GetMessageSender(destination, serviceBusClient);

                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.DeliveryConstraints, partitionKey);
                    ApplyCustomizationToOutgoingNativeMessage(context, operation, message);
                    dispatchTasks.Add(DispatchOperation(sender, message, transactionToUse));
                }
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