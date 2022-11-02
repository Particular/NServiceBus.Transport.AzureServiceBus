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
        static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
        static readonly Dictionary<string, List<IOutgoingTransportOperation>> emptyDestinationAndOperations = new();

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
            var numberOfIsolatedOperations = 0;
            var numberOfDefaultOperationDestinations = 0;

            foreach (var operation in transportOperations)
            {
                var destination = operation.ExtractDestination(defaultMulticastRoute: topicName);
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
                foreach (var operation in operations)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, azureServiceBusTransportTransaction?.IncomingQueuePartitionKey);
                    operation.ApplyCustomizationToOutgoingNativeMessage(message, transportTransaction, Log);
                    messagesToSend.Enqueue(message);
                }
                // Accessing azureServiceBusTransaction.CommittableTransaction will initialize it if it isn't yet
                // doing the access as late as possible but still on the synchronous path.
                dispatchTasks.Add(DispatchBatchForDestination(destination, azureServiceBusTransportTransaction?.ServiceBusClient, azureServiceBusTransportTransaction?.Transaction, messagesToSend, cancellationToken));
            }
        }

        async Task DispatchBatchForDestination(string destination, ServiceBusClient? client, Transaction? transaction, Queue<ServiceBusMessage> messagesToSend, CancellationToken cancellationToken)
        {
            var messageCount = messagesToSend.Count;
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

                var peekedMessage = messagesToSend.Peek();
                if (messageBatch.TryAddMessage(peekedMessage))
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
                    peekedMessage.ApplicationProperties.TryGetValue(Headers.MessageId, out var messageId);
                    var message =
                        @$"Unable to add the message '#{messageCount - messagesToSend.Count}' with message id '{messageId ?? peekedMessage.MessageId}' to the the batch '#{batchCount}'.
The message may be too large or the batch size has reached the maximum allowed messages per batch for the current tier selected for the namespace '{sender.FullyQualifiedNamespace}'. 
To mitigate this problem reduce the message size by using the data bus or upgrade to a higher Service Bus tier.";
                    throw new ServiceBusException(message, ServiceBusFailureReason.MessageSizeExceeded);
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
                    dispatchTasks.Add(DispatchIsolatedForDestination(destination, azureServiceBusTransportTransaction?.ServiceBusClient, noTransaction, message, cancellationToken));
                }
            }
        }

        async Task DispatchIsolatedForDestination(string destination, ServiceBusClient? client, Transaction? transaction, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            // Making sure we have a suppress scope around the sending
            using var scope = transaction.ToScope();
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            scope.Complete();
        }
    }
}