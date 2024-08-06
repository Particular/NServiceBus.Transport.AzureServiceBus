#nullable enable

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
        const int MaxMessageThresholdForTransaction = 100;

        static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
        static readonly Dictionary<string, List<IOutgoingTransportOperation>> emptyDestinationAndOperations = [];

        readonly MessageSenderRegistry messageSenderRegistry;
        readonly string topicName;

        public MessageDispatcher(MessageSenderRegistry messageSenderRegistry, string topicName)
        {
            this.messageSenderRegistry = messageSenderRegistry;
            this.topicName = topicName;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            _ = transaction.TryGet(out AzureServiceBusTransportTransaction azureServiceBusTransaction);

            var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
            var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

            var transportOperations =
                new List<IOutgoingTransportOperation>(unicastTransportOperations.Count +
                                                      multicastTransportOperations.Count);
            transportOperations.AddRange(unicastTransportOperations);
            transportOperations.AddRange(multicastTransportOperations);

            Dictionary<string, List<IOutgoingTransportOperation>>? isolatedOperationsPerDestination = null;
            Dictionary<string, List<IOutgoingTransportOperation>>? defaultOperationsPerDestination = null;
            var numberOfDefaultOperations = 0;
            var numberOfIsolatedOperations = 0;
            var numberOfDefaultOperationDestinations = 0;

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
                            defaultOperationsPerDestination[destination] = [operation];
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
                            isolatedOperationsPerDestination[destination] = [operation];
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

            if (azureServiceBusTransaction is { HasTransaction: true } && numberOfDefaultOperations > MaxMessageThresholdForTransaction)
            {
                throw new Exception($"The number of outgoing messages ({numberOfDefaultOperations}) exceeds the limits permitted by Azure Service Bus ({MaxMessageThresholdForTransaction}) in a single transaction");
            }

            var concurrentDispatchTasks =
                new List<Task>(numberOfIsolatedOperations + numberOfDefaultOperationDestinations);
            AddIsolatedOperationsTo(concurrentDispatchTasks, isolatedOperationsPerDestination ?? emptyDestinationAndOperations, transaction, azureServiceBusTransaction, cancellationToken);
            AddBatchedOperationsTo(concurrentDispatchTasks, defaultOperationsPerDestination ?? emptyDestinationAndOperations, transaction, azureServiceBusTransaction, cancellationToken);

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

        // The parameters of this method are deliberately mutable and of the original collection type to make sure
        // no boxing occurs
        void AddBatchedOperationsTo(List<Task> dispatchTasks,
            Dictionary<string, List<IOutgoingTransportOperation>> transportOperationsPerDestination,
            TransportTransaction transportTransaction,
            AzureServiceBusTransportTransaction? azureServiceBusTransportTransaction, CancellationToken cancellationToken)
        {
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var operations = destinationAndOperations.Value;

                var messagesToSend = new Queue<ServiceBusMessage>(operations.Count);
                // We assume the majority of the messages will be batched and only a few will be sent individually
                // and therefore it is OK in those rare cases for the list to grow.
                var messagesTooLargeToBeBatched = new List<ServiceBusMessage>(0);
                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, azureServiceBusTransportTransaction?.IncomingQueuePartitionKey);
                    operation.ApplyCustomizationToOutgoingNativeMessage(message, transportTransaction, Log);
                    messagesToSend.Enqueue(message);
                }
                // Accessing azureServiceBusTransaction.CommittableTransaction will initialize it if it isn't yet
                // doing the access as late as possible but still on the synchronous path.
                dispatchTasks.Add(DispatchBatchForDestination(destination, azureServiceBusTransportTransaction?.ServiceBusClient, azureServiceBusTransportTransaction?.Transaction, messagesToSend, messagesTooLargeToBeBatched, cancellationToken));

                foreach (var message in messagesTooLargeToBeBatched)
                {
                    dispatchTasks.Add(DispatchForDestination(destination, azureServiceBusTransportTransaction?.ServiceBusClient, azureServiceBusTransportTransaction?.Transaction, message, cancellationToken));
                }
            }
        }

        async Task DispatchBatchForDestination(string destination, ServiceBusClient? client, Transaction? transaction,
            Queue<ServiceBusMessage> messagesToSend, List<ServiceBusMessage> messagesTooLargeToBeBatched,
            CancellationToken cancellationToken)
        {
            int batchCount = 0;
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            while (messagesToSend.Count > 0)
            {
                using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken)
                    .ConfigureAwait(false);

                StringBuilder? logBuilder = null;
                if (Log.IsDebugEnabled)
                {
                    logBuilder = new StringBuilder();
                }

                var dequeueMessage = messagesToSend.Dequeue();
                if (messageBatch.TryAddMessage(dequeueMessage))
                {
                    if (Log.IsDebugEnabled)
                    {
                        dequeueMessage.ApplicationProperties.TryGetValue(Headers.MessageId, out var messageId);
                        logBuilder!.Append($"{messageId ?? dequeueMessage.MessageId},");
                    }
                }
                else
                {
                    if (Log.IsDebugEnabled)
                    {
                        dequeueMessage.ApplicationProperties.TryGetValue(Headers.MessageId, out var messageId);
                        Log.Debug($"Message '{messageId ?? dequeueMessage.MessageId}' is too large for the batch '{batchCount}' and will be sent individually to destination {destination}.");
                    }
                    messagesTooLargeToBeBatched.Add(dequeueMessage);
                    continue;
                }

                // Trying to add as many messages as we can to the batch. TryAdd might return false due to the batch being full
                // or the message being too large. In the case when the message is too large for the batch the next iteration
                // will try to add it to a fresh batch and if that fails too we will add it to the list of messages that couldn't be sent
                // trying to attempt to send them individually.
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

                using var scope = transaction.ToScope();
                await sender.SendMessagesAsync(messageBatch, cancellationToken).ConfigureAwait(false);
                //committable tx will not be committed because this scope is not the owner
                scope.Complete();

                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Sent batch '{batchCount}' with '{messageBatch.Count}' message ids '{logBuilder!.ToString(0, logBuilder.Length - 1)}' to destination {destination}.");
                }
            }
        }

        // The parameters of this method are deliberately mutable and of the original collection type to make sure
        // no boxing occurs
        void AddIsolatedOperationsTo(List<Task> dispatchTasks,
            Dictionary<string, List<IOutgoingTransportOperation>> transportOperationsPerDestination,
            TransportTransaction transportTransaction,
            AzureServiceBusTransportTransaction? azureServiceBusTransportTransaction,
            CancellationToken cancellationToken)
        {
            // It is OK to use the pumps client and partition key (keeps things compliant as before) but
            // isolated dispatches should never use the committable transaction regardless whether it is present
            // or not.
            Transaction? noTransaction = default;
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var operations = destinationAndOperations.Value;

                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, azureServiceBusTransportTransaction?.IncomingQueuePartitionKey);
                    operation.ApplyCustomizationToOutgoingNativeMessage(message, transportTransaction, Log);
                    dispatchTasks.Add(DispatchForDestination(destination, azureServiceBusTransportTransaction?.ServiceBusClient, noTransaction, message, cancellationToken));
                }
            }
        }

        async Task DispatchForDestination(string destination, ServiceBusClient? client, Transaction? transaction, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            // Making sure we have a suppress scope around the sending
            using var scope = transaction.ToScope();
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            scope.Complete();
        }
    }
}
