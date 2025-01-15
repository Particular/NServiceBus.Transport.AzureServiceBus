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
        static readonly Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)> emptyDestinationAndOperations = [];

        readonly MessageSenderRegistry messageSenderRegistry;
        readonly bool doNotSendTransportEncodingHeader;
        readonly OutgoingNativeMessageCustomizationAction customizerCallback;
        readonly EventRoutingCache eventRoutingCache;

        public MessageDispatcher(
            MessageSenderRegistry messageSenderRegistry,
            TopologyOptions topologyOptions,
            OutgoingNativeMessageCustomizationAction? customizerCallback = null,
            bool doNotSendTransportEncodingHeader = false
        )
        {
            this.messageSenderRegistry = messageSenderRegistry;
            this.customizerCallback = customizerCallback ?? (static (_, _) => { }); // Noop callback to not require a null check
            this.doNotSendTransportEncodingHeader = doNotSendTransportEncodingHeader;
            // Maybe pass the cache in from the outside?
            eventRoutingCache = new EventRoutingCache(topologyOptions);
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

            Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)>? isolatedOperationsPerDestination = null;
            Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)>? defaultOperationsPerDestination = null;
            var numberOfDefaultOperations = 0;
            var numberOfIsolatedOperations = 0;

            foreach (var operation in transportOperations)
            {
                var destination = operation.ExtractDestination(eventRoutingCache);
                switch (operation.RequiredDispatchConsistency)
                {
                    case DispatchConsistency.Default:
                        numberOfDefaultOperations++;
                        defaultOperationsPerDestination ??=
                            new Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)>(StringComparer.OrdinalIgnoreCase);

                        if (!defaultOperationsPerDestination.ContainsKey(destination))
                        {
                            defaultOperationsPerDestination[destination] = (operation is MulticastTransportOperation, [operation]);
                        }
                        else
                        {
                            defaultOperationsPerDestination[destination].Operations.Add(operation);
                        }
                        break;
                    case DispatchConsistency.Isolated:
                        // every isolated operation counts
                        numberOfIsolatedOperations++;
                        isolatedOperationsPerDestination ??=
                            new Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)>(StringComparer.OrdinalIgnoreCase);
                        if (!isolatedOperationsPerDestination.ContainsKey(destination))
                        {
                            isolatedOperationsPerDestination[destination] = (operation is MulticastTransportOperation, [operation]);
                        }
                        else
                        {
                            isolatedOperationsPerDestination[destination].Operations.Add(operation);
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

            Task[] dispatchTasks =
            [
                DispatchIsolatedOperations(isolatedOperationsPerDestination ?? emptyDestinationAndOperations, numberOfIsolatedOperations, transaction, azureServiceBusTransaction, cancellationToken),
                DispatchBatchedOperations(defaultOperationsPerDestination ?? emptyDestinationAndOperations, numberOfDefaultOperations, transaction, azureServiceBusTransaction, cancellationToken)
            ];

            try
            {
                await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Log.Error("Exception from Send.", ex);
                throw;
            }
        }

        // The parameters of this method are deliberately mutable and of the original collection type to make sure
        // no boxing occurs
        Task DispatchBatchedOperations(
            Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)> transportOperationsPerDestination,
            int numberOfTransportOperations,
            TransportTransaction transportTransaction,
            AzureServiceBusTransportTransaction? azureServiceBusTransportTransaction,
            CancellationToken cancellationToken
        )
        {
            if (numberOfTransportOperations == 0)
            {
                return Task.CompletedTask;
            }

            var dispatchTasks = new List<Task>(transportOperationsPerDestination.Count);
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var (isTopic, operations) = destinationAndOperations.Value;

                var messagesToSend = new Queue<ServiceBusMessage>(operations.Count);
                foreach (var operation in operations)
                {
                    var message = operation.ToAzureServiceBusMessage(azureServiceBusTransportTransaction?.IncomingQueuePartitionKey, doNotSendTransportEncodingHeader);
                    operation.ApplyCustomizationToOutgoingNativeMessage(message, transportTransaction, Log);
                    customizerCallback(operation, message);

                    messagesToSend.Enqueue(message);
                }
                // Accessing azureServiceBusTransaction.CommittableTransaction will initialize it if it isn't yet
                // doing the access as late as possible but still on the synchronous path. Initializing the transaction
                // as late as possible is important because it will start the transaction timer. If the transaction
                // is started too early it might shorten the overall transaction time available.
                dispatchTasks.Add(DispatchBatchOrFallbackToIndividualSendsForDestination(destination, isTopic, azureServiceBusTransportTransaction?.ServiceBusClient, azureServiceBusTransportTransaction?.Transaction, messagesToSend, cancellationToken));
            }
            return Task.WhenAll(dispatchTasks);
        }

        async Task DispatchBatchOrFallbackToIndividualSendsForDestination(string destination, bool isTopic, ServiceBusClient? client, Transaction? transaction,
            Queue<ServiceBusMessage> messagesToSend,
            CancellationToken cancellationToken)
        {
            int batchCount = 0;
            List<ServiceBusMessage>? messagesTooLargeToBeBatched = null;
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            while (messagesToSend.Count > 0)
            {
                try
                {
                    using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken)
                        .ConfigureAwait(false);

                    StringBuilder? logBuilder = null;
                    if (Log.IsDebugEnabled)
                    {
                        logBuilder = new StringBuilder();
                    }

                    var dequeueMessage = messagesToSend.Dequeue();
                    // In this case the batch is fresh and doesn't have any messages yet. If TryAdd returns false
                    // we know the message can never be added to any batch and therefore we collect it to be sent
                    // individually.
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
                        messagesTooLargeToBeBatched ??= [];
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
                // The catch is deliberately at this level because CreateMessageBatchAsync might also throw
                // when it tries to establish a link to a non-existing entity.
                catch (ServiceBusException e) when (isTopic && e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    Log.Debug($"Skipping sending messages to topic {destination} because the destination does not exist.");
                    return;
                }
            }

            if (messagesTooLargeToBeBatched is not null)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Sending '{messagesTooLargeToBeBatched.Count}' that were too large for the batch individually to destination {destination}.");
                }

                var individualSendTasks = new List<Task>(messagesTooLargeToBeBatched.Count);
                foreach (var message in messagesTooLargeToBeBatched)
                {
                    individualSendTasks.Add(DispatchForDestination(destination, isTopic, client, transaction, message, cancellationToken));
                }

                await Task.WhenAll(individualSendTasks)
                    .ConfigureAwait(false);

                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Sent '{messagesTooLargeToBeBatched.Count}' that were too large for the batch individually to destination {destination}.");
                }
            }
        }

        // The parameters of this method are deliberately mutable and of the original collection type to make sure
        // no boxing occurs
        Task DispatchIsolatedOperations(
            Dictionary<string, (bool IsTopic, List<IOutgoingTransportOperation> Operations)> transportOperationsPerDestination,
            int numberOfTransportOperations,
            TransportTransaction transportTransaction,
            AzureServiceBusTransportTransaction? azureServiceBusTransportTransaction,
            CancellationToken cancellationToken)
        {
            if (numberOfTransportOperations == 0)
            {
                return Task.CompletedTask;
            }

            // It is OK to use the pumps client and partition key (keeps things compliant as before) but
            // isolated dispatches should never use the committable transaction regardless whether it is present
            // or not.
            Transaction? noTransaction = null;
            var dispatchTasks = new List<Task>(numberOfTransportOperations);
            foreach (var destinationAndOperations in transportOperationsPerDestination)
            {
                var destination = destinationAndOperations.Key;
                var (isTopic, operations) = destinationAndOperations.Value;

                foreach (var operation in operations)
                {
                    var message = operation.ToAzureServiceBusMessage(azureServiceBusTransportTransaction?.IncomingQueuePartitionKey, doNotSendTransportEncodingHeader);
                    operation.ApplyCustomizationToOutgoingNativeMessage(message, transportTransaction, Log);
                    customizerCallback(operation, message);
                    dispatchTasks.Add(DispatchForDestination(destination, isTopic, azureServiceBusTransportTransaction?.ServiceBusClient, noTransaction, message, cancellationToken));
                }
            }

            return Task.WhenAll(dispatchTasks);
        }

        async Task DispatchForDestination(string destination, bool isTopic, ServiceBusClient? client,
            Transaction? transaction, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            var sender = messageSenderRegistry.GetMessageSender(destination, client);
            try
            {
                // Making sure we have a suppress scope around the sending
                using var scope = transaction.ToScope();
                await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                scope.Complete();
            }
            catch (ServiceBusException e) when (isTopic && e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                Log.Debug($"Sending message with message ID '{message.MessageId}' to topic {destination} failed because the destination does not exist.");
            }
        }
    }
}