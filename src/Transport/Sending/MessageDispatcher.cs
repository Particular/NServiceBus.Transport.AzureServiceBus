namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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

            var concurrentDispatchTasks = new List<Task>(2);

            foreach (var dispatchConsistencyGroup in transportOperations
                         .GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks.Add(DispatchIsolated(dispatchConsistencyGroup, client, partitionKey, transaction, cancellationToken));
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks.Add(DispatchBatched(dispatchConsistencyGroup, client, partitionKey, committableTransaction, transaction, cancellationToken));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

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

        Task DispatchBatched(IEnumerable<IOutgoingTransportOperation> dispatchConsistencyGroup, ServiceBusClient client, string partitionKey, CommittableTransaction committableTransaction, TransportTransaction transaction, CancellationToken cancellationToken)
        {
            var dispatchTasks = new List<Task>();
            foreach (var operationsPerDestination in dispatchConsistencyGroup.Select(op => (Destination: Destination(op), Operation: op))
                         .GroupBy(destOp => destOp.Destination, StringComparer.OrdinalIgnoreCase))
            {
                var messagesToSend = new Queue<ServiceBusMessage>();
                foreach (var (_, operation) in operationsPerDestination)
                {
                    var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, partitionKey);
                    ApplyCustomizationToOutgoingNativeMessage(operation, message, transaction);
                    messagesToSend.Enqueue(message);
                }
                dispatchTasks.Add(DispatchBatchForDestination(operationsPerDestination.Key, client, committableTransaction, messagesToSend, cancellationToken));
            }
            return Task.WhenAll(dispatchTasks);
        }

        async Task DispatchBatchForDestination(string destination, ServiceBusClient client, CommittableTransaction committableTransaction, Queue<ServiceBusMessage> messagesToSend, CancellationToken cancellationToken)
        {
            var messageCount = messagesToSend.Count;
            int batchCount = 0;
            // TODO: Currently this design creates more senders because we are no longer quickly returning them. Planning to change the sender pool in a dedicated commit
            var sender = messageSenderPool.GetMessageSender(destination, client);
            try
            {
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
            finally
            {
                messageSenderPool.ReturnMessageSender(sender, client);
            }
        }

        Task DispatchIsolated(IEnumerable<IOutgoingTransportOperation> dispatchConsistencyGroup, ServiceBusClient client, string partitionKey, TransportTransaction transaction, CancellationToken cancellationToken)
        {
            var dispatchTasks = new List<Task>();
            foreach (var (destination, operation) in dispatchConsistencyGroup.Select(op => (Destination: Destination(op), Operation: op)))
            {
                var message = operation.Message.ToAzureServiceBusMessage(operation.Properties, partitionKey);
                ApplyCustomizationToOutgoingNativeMessage(operation, message, transaction);
                dispatchTasks.Add(DispatchIsolatedForDestination(destination, client, message, cancellationToken));
            }
            return dispatchTasks.Count == 1 ? dispatchTasks[0] : Task.WhenAll(dispatchTasks);
        }

        async Task DispatchIsolatedForDestination(string destination, ServiceBusClient client, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            // TODO: Currently this design creates more senders because we are no longer quickly returning them. Planning to change the sender pool in a dedicated commit
            var sender = messageSenderPool.GetMessageSender(destination, client);
            try
            {
                using var scope = default(Transaction).ToScope();
                await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                //committable tx will not be committed because this scope is not the owner
                scope.Complete();
            }
            finally
            {
                messageSenderPool.ReturnMessageSender(sender, client);
            }
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