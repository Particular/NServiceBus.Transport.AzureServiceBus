namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Logging;

    class MessageDispatcher : IMessageDispatcher
    {
        static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
        static readonly Dictionary<string, List<IOutgoingTransportOperation>> emptyDestinationAndOperations =
            new Dictionary<string, List<IOutgoingTransportOperation>>();

        readonly MessageSenderRegistry messageSenderRegistry;
        readonly string topicName;

        public MessageDispatcher(MessageSenderRegistry messageSenderRegistry, string topicName)
        {
            this.messageSenderRegistry = messageSenderRegistry;
            this.topicName = topicName;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            transaction.TryGet<ServiceBusClient>(out var client);
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
            var numberOfDefaultOperationDestinations = 0;

            foreach (var operation in transportOperations)
            {
                var destination = Destination(operation);
                switch (operation.RequiredDispatchConsistency)
                {
                    case DispatchConsistency.Default:
                        defaultOperationsPerDestination ??=
                            new Dictionary<string, List<IOutgoingTransportOperation>>(StringComparer.OrdinalIgnoreCase);

                        if (!defaultOperationsPerDestination.ContainsKey(destination))
                        {
                            defaultOperationsPerDestination[destination] = new List<IOutgoingTransportOperation> { operation };
                            // because we batch only the number of destinations are relevant
                            numberOfDefaultOperationDestinations++;
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

            var concurrentDispatchTasks =
                new List<Task>(numberOfIsolatedOperations + numberOfDefaultOperationDestinations);
            AddIsolatedOperationsTo(concurrentDispatchTasks, isolatedOperationsPerDestination ?? emptyDestinationAndOperations, client, partitionKey, transaction, cancellationToken);
            AddBatchedOperationsTo(concurrentDispatchTasks, defaultOperationsPerDestination ?? emptyDestinationAndOperations, client, partitionKey, committableTransaction, transaction, cancellationToken);

            try
            {
                await Task.WhenAll(concurrentDispatchTasks).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Log.Error("Exception from Send.", ex);
                throw;
            }
        }

        void AddBatchedOperationsTo(List<Task> dispatchTasks,
            Dictionary<string, List<IOutgoingTransportOperation>> transportOperationsPerDestination,
            ServiceBusClient client, string partitionKey, CommittableTransaction committableTransaction,
            TransportTransaction transaction, CancellationToken cancellationToken)
        {
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var operations = destinationAndOperations.Value;

                var messagesToSend = new Queue<ServiceBusMessage>(operations.Count);
                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, partitionKey);
                    ApplyCustomizationToOutgoingNativeMessage(operation, message, transaction);
                    messagesToSend.Enqueue(message);
                }
                dispatchTasks.Add(DispatchBatchForDestination(destination, client, committableTransaction, messagesToSend, cancellationToken));
            }
        }

        async Task DispatchBatchForDestination(string destination, ServiceBusClient client, CommittableTransaction committableTransaction, Queue<ServiceBusMessage> messagesToSend, CancellationToken cancellationToken)
        {
            var messageCount = messagesToSend.Count;
            int batchCount = 0;
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            while (messagesToSend.Count > 0)
            {
                using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken)
                    .ConfigureAwait(false);

                StringBuilder logBuilder = null;
                if (Log.IsDebugEnabled)
                {
                    logBuilder = new StringBuilder();
                }

                if (messageBatch.TryAddMessage(messagesToSend.Peek()))
                {
                    var added = messagesToSend.Dequeue();
                    if (Log.IsDebugEnabled)
                    {
                        added.ApplicationProperties.TryGetValue(Headers.MessageId, out var messageId);
                        logBuilder!.Append($"{messageId ?? added.MessageId},");
                    }
                }
                else
                {
                    throw new Exception($"Message {messageCount - messagesToSend.Count} is too large and cannot be sent.");
                }

                while (messagesToSend.Count > 0 && messageBatch.TryAddMessage(messagesToSend.Peek()))
                {
                    var added = messagesToSend.Dequeue();
                    if (Log.IsDebugEnabled)
                    {
                        added.ApplicationProperties.TryGetValue(Headers.MessageId, out var messageId);
                        logBuilder!.Append($"{messageId ?? added.MessageId},");
                    }
                }

                batchCount++;
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Sending batch '{batchCount}' with '{messageBatch.Count}' message ids '{logBuilder!.ToString(0, logBuilder.Length - 1)}' to destination {destination}.");
                }

                using var scope = committableTransaction.ToScope();
                await sender.SendMessagesAsync(messageBatch, cancellationToken).ConfigureAwait(false);
                //committable tx will not be committed because this scope is not the owner
                scope.Complete();

                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Sent batch '{batchCount}' with '{messageBatch.Count}' message ids '{logBuilder!.ToString(0, logBuilder.Length - 1)}' to destination {destination}.");
                }
            }
        }

        void AddIsolatedOperationsTo(List<Task> dispatchTasks,
            Dictionary<string, List<IOutgoingTransportOperation>> transportOperationsPerDestination,
            ServiceBusClient client, string partitionKey, TransportTransaction transaction,
            CancellationToken cancellationToken)
        {
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var operations = destinationAndOperations.Value;

                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, partitionKey);
                    ApplyCustomizationToOutgoingNativeMessage(operation, message, transaction);
                    dispatchTasks.Add(DispatchIsolatedForDestination(destination, client, message, cancellationToken));
                }
            }
        }

        async Task DispatchIsolatedForDestination(string destination, ServiceBusClient client, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            // Making sure we have a suppress scope around the sending
            using var scope = default(Transaction).ToScope();
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            //committable tx will not be committed because this scope is not the owner
            scope.Complete();
        }

        string Destination(IOutgoingTransportOperation outgoingTransportOperation)
        {
            switch (outgoingTransportOperation)
            {
                case MulticastTransportOperation:
                    return topicName;
                case UnicastTransportOperation unicastTransportOperation:
                    var destination = unicastTransportOperation.Destination;

                    // Workaround for reply-to address set by ASB transport
                    var index = unicastTransportOperation.Destination.IndexOf('@');

                    if (index > 0)
                    {
                        destination = destination.Substring(0, index);
                    }
                    return destination;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
            }
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