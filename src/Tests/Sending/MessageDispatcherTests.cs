namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
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

        [Test]
        public void Should_throw_when_batch_size_exceeded()
        {
            var client = new FakeServiceBusClient();
            var sender = new FakeSender();
            client.Senders["SomeDestination"] = sender;

            sender.TryAdd = _ => false;

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            Assert.ThrowsAsync<ServiceBusException>(async () => await dispatcher.Dispatch(new TransportOperations(operation1), new TransportTransaction()));
        }

        [Test]
        public async Task Should_use_connection_information_of_existing_service_bus_transaction()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;
            var transactionalClient = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(defaultClient), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction(transactionalClient,
                "SomePartitionKey", new TransactionOptions());

            await dispatcher.Dispatch(new TransportOperations(operation1), azureServiceBusTransaction.TransportTransaction);

            var transactionalSender = transactionalClient.Senders["SomeDestination"];

            Assert.That(transactionalSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someDestinationTransactionalBatchContent = transactionalSender[transactionalSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someDestinationTransactionalBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == "SomePartitionKey"));
            Assert.That(defaultSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.Transaction, Is.Not.Null);
        }

        [Test]
        public async Task Should_split_into_batches_of_max_hundred_when_transactions_used()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;
            var transactionalSender = new FakeSender();
            var transactionalClient = new FakeServiceBusClient();
            transactionalClient.Senders["SomeDestination"] = transactionalSender;

            bool firstTime = true;
            transactionalSender.TryAdd = msg =>
            {
                if ((string)msg.ApplicationProperties["Number"] != "125" || !firstTime)
                {
                    return true;
                }

                firstTime = false;
                return false;
            };

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(defaultClient), "sometopic");

            var operations = new List<TransportOperation>(150);
            for (int i = 0; i < 150; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction(transactionalClient,
                "SomePartitionKey", new TransactionOptions());

            await dispatcher.Dispatch(new TransportOperations(operations.ToArray()), azureServiceBusTransaction.TransportTransaction);

            Assert.That(transactionalSender.BatchSentMessages, Has.Count.EqualTo(3));
            var someDestinationTransactionalBatchContent1 = transactionalSender[transactionalSender.BatchSentMessages.ElementAt(0)];
            var someDestinationTransactionalBatchContent2 = transactionalSender[transactionalSender.BatchSentMessages.ElementAt(1)];
            var someDestinationTransactionalBatchContent3 = transactionalSender[transactionalSender.BatchSentMessages.ElementAt(2)];
            Assert.That(someDestinationTransactionalBatchContent1, Has.Count.EqualTo(100));
            Assert.That(someDestinationTransactionalBatchContent2, Has.Count.EqualTo(25));
            Assert.That(someDestinationTransactionalBatchContent2, Has.Count.EqualTo(25));
            Assert.That(defaultSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.Transaction, Is.Not.Null);
        }

        [Test]
        public async Task Should_use_default_connection_information_when_existing_service_bus_transaction_has_none()
        {
            var defaultClient = new FakeServiceBusClient();
            var transactionalClient = new FakeServiceBusClient();
            var transactionalSender = new FakeSender();
            transactionalClient.Senders["SomeDestination"] = transactionalSender;

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(defaultClient), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            await dispatcher.Dispatch(new TransportOperations(operation1), azureServiceBusTransaction.TransportTransaction);

            var defaultSender = defaultClient.Senders["SomeDestination"];

            Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = defaultSender[defaultSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == default));
            Assert.That(transactionalSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.Transaction, Is.Null);
        }

        class SomeEvent { }
        class SomeOtherEvent { }
    }
}