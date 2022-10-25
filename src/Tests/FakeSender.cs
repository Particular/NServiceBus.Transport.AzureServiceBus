namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeSender : ServiceBusSender
    {
        readonly List<ServiceBusMessage> sentMessages = new List<ServiceBusMessage>();
        readonly List<ServiceBusMessageBatch> batchedMessages = new List<ServiceBusMessageBatch>();
        readonly ConditionalWeakTable<ServiceBusMessageBatch, IReadOnlyCollection<ServiceBusMessage>>
            batchToBackingStore =
                new ConditionalWeakTable<ServiceBusMessageBatch, IReadOnlyCollection<ServiceBusMessage>>();

        public IReadOnlyCollection<ServiceBusMessage> IndividuallySentMessages => sentMessages;
        public IReadOnlyCollection<ServiceBusMessageBatch> BatchSentMessages => batchedMessages;
        public Func<ServiceBusMessage, bool> TryAdd { get; set; } = _ => true;

        public override string FullyQualifiedNamespace { get; } = "FullyQualifiedNamespace";

        public IReadOnlyCollection<ServiceBusMessage> this[ServiceBusMessageBatch batch]
        {
            get => batchToBackingStore.TryGetValue(batch, out var store) ? store : Array.Empty<ServiceBusMessage>();
            set => throw new NotSupportedException();
        }

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
        {
            var batchMessageStore = new List<ServiceBusMessage>();
            ServiceBusMessageBatch serviceBusMessageBatch = ServiceBusModelFactory.ServiceBusMessageBatch(256 * 1024, batchMessageStore, tryAddCallback: TryAdd);
            batchToBackingStore.Add(serviceBusMessageBatch, batchMessageStore);
            return new ValueTask<ServiceBusMessageBatch>(
                serviceBusMessageBatch);
        }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentMessages.Add(message);
            return Task.CompletedTask;
        }

        public override Task SendMessagesAsync(ServiceBusMessageBatch messageBatch, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batchedMessages.Add(messageBatch);
            return Task.CompletedTask;
        }
    }
}