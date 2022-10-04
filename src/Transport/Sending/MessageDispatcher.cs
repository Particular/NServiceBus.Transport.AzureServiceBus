namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Logging;

    class MessageDispatcher : IMessageDispatcher
    {
        static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
        readonly MessageSenderPool messageSenderPool;
        readonly string topicName;

        public MessageDispatcher(MessageSenderPool messageSenderPool, string topicName)
        {
            this.messageSenderPool = messageSenderPool;
            this.topicName = topicName;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            transaction.TryGet<ServiceBusClient>(out var client);
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

                var sender = messageSenderPool.GetMessageSender(destination, client);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.Properties, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(transportOperation, message, transaction);

                    var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendMessageAsync(message, cancellationToken));
                        //committable tx will not be committed because this scope is not the owner
                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, client);
                }
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                var sender = messageSenderPool.GetMessageSender(topicName, client);

                try
                {
                    var message = transportOperation.Message.ToAzureServiceBusMessage(transportOperation.Properties, partitionKey);

                    ApplyCustomizationToOutgoingNativeMessage(transportOperation, message, transaction);

                    var transactionToUse = transportOperation.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction;
                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        tasks.Add(sender.SendMessageAsync(message, cancellationToken));
                        //committable tx will not be committed because this scope is not the owner
                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, client);
                }
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        static void ApplyCustomizationToOutgoingNativeMessage(IOutgoingTransportOperation transportOperation,
            ServiceBusMessage message, TransportTransaction transportTransaction)
        {
            if (!transportOperation.Properties.TryGetValue(NativeMessageCustomizationBehavior.CustomizationKey,
                out var key))
            {
                return;
            }

            var messageCustomizer = transportTransaction.Get<NativeMessageCustomizer>();
            if (!messageCustomizer.Customizations.TryGetValue(key, out var action))
            {
                Log.Warn(
                    $"Message {transportOperation.Message.MessageId} was configured with a native message customization but the customization was not found in {nameof(NativeMessageCustomizer)}");
                return;
            }

            action(message);
        }
    }
}