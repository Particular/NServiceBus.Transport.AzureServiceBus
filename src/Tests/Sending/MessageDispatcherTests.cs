namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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
        public async Task Should_dispatch_multicast_isolated_dispatches_individually()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

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
        public async Task Should_dispatch_multicast_isolated_dispatches_individually_regardless_of_the_event_to_the_topic()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Is.Empty);
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
        public async Task Should_dispatch_multicast_default_dispatches_together_as_batch()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

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

        [Test]
        public async Task Should_dispatch_multicast_default_dispatches_together_as_batch_regardless_of_the_event_to_the_topic()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.That(sender.IndividuallySentMessages, Is.Empty);
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            Assert.That(batchContent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Should_allow_mixing_operations()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("Operation1",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("Operation2",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Isolated);

            var operation3 =
                new TransportOperation(new OutgoingMessage("Operation3",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var operation4 =
                new TransportOperation(new OutgoingMessage("Operation4",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, operation3, operation4), new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];
            var someTopicSender = client.Senders["sometopic"];

            Assert.That(someDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
            Assert.That(someDestinationSender.BatchSentMessages, Is.Empty);

            Assert.That(someOtherDestinationSender.IndividuallySentMessages, Is.Empty);
            Assert.That(someOtherDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = someOtherDestinationSender[someOtherDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Count.EqualTo(1));

            Assert.That(someTopicSender.IndividuallySentMessages, Has.Count.EqualTo(1));
            Assert.That(someTopicSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someTopicSenderBatchContent = someTopicSender[someTopicSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someTopicSenderBatchContent, Has.Count.EqualTo(1));
        }

        class SomeEvent { }
        class SomeOtherEvent { }
    }
}