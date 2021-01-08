namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;

    class MessageDispatcher : IMessageDispatcher
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

                var receiverConnectionAndPathToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? (null, null) : receiverConnectionAndPath;
                var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;

                var sender = messageSenderPool.GetMessageSender(destination, receiverConnectionAndPathToUse);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendAsync(message));

                        scope.Complete();
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
                var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;

                var sender = messageSenderPool.GetMessageSender(topicName, receiverConnectionAndPathToUse);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.DeliveryConstraints, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(context, transportOperation, message);

                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendAsync(message));
                        //committable tx will not be committed because this scope is not the owner
                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender);
                }
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        private static void ApplyCustomizationToOutgoingNativeMessage(ReadOnlyContextBag context, IOutgoingTransportOperation transportOperation, Message message)
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

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new System.NotImplementedException();
        }
    }
}