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
        readonly List<ServiceBusMessage> sentMessages = [];
        readonly List<ServiceBusMessageBatch> batchedMessages = [];
        readonly ConditionalWeakTable<ServiceBusMessageBatch, IReadOnlyCollection<ServiceBusMessage>>
            batchToBackingStore =
                [];

        public IReadOnlyCollection<ServiceBusMessage> IndividuallySentMessages => sentMessages;
        public IReadOnlyCollection<ServiceBusMessageBatch> BatchSentMessages => batchedMessages;
        public Func<ServiceBusMessage, bool> TryAdd { get; set; } = _ => true;
        public Action<ServiceBusMessage> SendMessageAction { get; set; } = _ => { };
        public Action<ServiceBusMessageBatch> SendMessageBatchAction { get; set; } = _ => { };

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
            return new ValueTask<ServiceBusMessageBatch>(serviceBusMessageBatch);
        }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendMessageAction(message);
            sentMessages.Add(message);
            return Task.CompletedTask;
        }

        public override Task SendMessagesAsync(ServiceBusMessageBatch messageBatch, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendMessageBatchAction(messageBatch);
            batchedMessages.Add(messageBatch);
            return Task.CompletedTask;
        }
    }
}