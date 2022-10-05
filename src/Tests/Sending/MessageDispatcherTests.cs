namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NUnit.Framework;
    using Routing;

    [TestFixture]
    public class MessageDispatcherTests
    {
        [Test]
        public async Task Should_dispatch_unicast_isolated_dispatches_individually()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["SomeDestination"];

            Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Is.Empty);
        }

        [Test]
        public async Task Should_dispatch_unicast_isolated_dispatches_individually_per_destination()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];

            Assert.That(someDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
            Assert.That(someDestinationSender.BatchSentMessages, Is.Empty);
            Assert.That(someOtherDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
            Assert.That(someOtherDestinationSender.BatchSentMessages, Is.Empty);
        }

        [Test]
        public async Task Should_dispatch_unicast_default_dispatches_together_as_batch()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["SomeDestination"];

            Assert.That(sender.IndividuallySentMessages, Is.Empty);
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            Assert.That(batchContent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Should_dispatch_unicast_default_dispatches_together_as_batch_per_destination()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];

            Assert.That(someDestinationSender.IndividuallySentMessages, Is.Empty);
            Assert.That(someDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someDestinationBatchContent = someDestinationSender[someDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someDestinationBatchContent, Has.Count.EqualTo(1));

            Assert.That(someOtherDestinationSender.IndividuallySentMessages, Is.Empty);
            Assert.That(someOtherDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = someOtherDestinationSender[someOtherDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Count.EqualTo(1));
        }
    }

    public class FakeSender : ServiceBusSender
    {
        readonly List<ServiceBusMessage> sentMessages = new List<ServiceBusMessage>();
        readonly List<ServiceBusMessageBatch> batchedMessages = new List<ServiceBusMessageBatch>();
        readonly ConditionalWeakTable<ServiceBusMessageBatch, IReadOnlyCollection<ServiceBusMessage>>
            batchToBackingStore =
                new ConditionalWeakTable<ServiceBusMessageBatch, IReadOnlyCollection<ServiceBusMessage>>();

        public IReadOnlyCollection<ServiceBusMessage> IndividuallySentMessages => sentMessages;
        public IReadOnlyCollection<ServiceBusMessageBatch> BatchSentMessages => batchedMessages;

        public IReadOnlyCollection<ServiceBusMessage> this[ServiceBusMessageBatch batch]
        {
            get => batchToBackingStore.TryGetValue(batch, out var store) ? store : Array.Empty<ServiceBusMessage>();
            set => throw new NotSupportedException();
        }

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
        {
            var batchMessageStore = new List<ServiceBusMessage>();
            ServiceBusMessageBatch serviceBusMessageBatch = ServiceBusModelFactory.ServiceBusMessageBatch(256 * 1024, batchMessageStore);
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

        public override Task SendMessagesAsync(ServiceBusMessageBatch messageBatch,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();
            batchedMessages.Add(messageBatch);
            return Task.CompletedTask;
        }
    }

    public class FakeServiceBusClient : ServiceBusClient
    {
        public Dictionary<string, FakeSender> Senders { get; } = new Dictionary<string, FakeSender>();

        public override ServiceBusSender CreateSender(string queueOrTopicName)
        {
            if (!Senders.ContainsKey(queueOrTopicName))
            {
                Senders[queueOrTopicName] = new FakeSender();
            }
            return Senders[queueOrTopicName];
        }
    }
}