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
        public async Task Should_use_connection_information_of_existing_service_bus_transaction_with_cross_entity_transaction()
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

            var transportTransaction = new TransportTransaction();
            var azureServiceBusTransaction = new AzureServiceBusTransaction(transportTransaction, useCrossEntityTransactions: true)
            {
                ServiceBusClient = transactionalClient,
                IncomingQueuePartitionKey = "SomePartitionKey"
            };
            transportTransaction.Set(azureServiceBusTransaction);

            await dispatcher.Dispatch(new TransportOperations(operation1), transportTransaction);

            var transactionalSender = transactionalClient.Senders["SomeDestination"];

            Assert.That(transactionalSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someDestinationTransactionalBatchContent = transactionalSender[transactionalSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someDestinationTransactionalBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == "SomePartitionKey"));
            Assert.That(defaultSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.CommittableTransaction, Is.Not.Null);
        }

        [Test]
        public async Task Should_default_connection_information_and_ignore_existing_service_bus_transaction_information_with_cross_entity_transaction_disabled()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;
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

            var transportTransaction = new TransportTransaction();
            var azureServiceBusTransaction = new AzureServiceBusTransaction(transportTransaction, useCrossEntityTransactions: false)
            {
                ServiceBusClient = transactionalClient,
                IncomingQueuePartitionKey = "SomePartitionKey"
            };
            transportTransaction.Set(azureServiceBusTransaction);

            await dispatcher.Dispatch(new TransportOperations(operation1), transportTransaction);

            Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = defaultSender[defaultSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == default));
            Assert.That(transactionalSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.CommittableTransaction, Is.Null);
            Assert.That(azureServiceBusTransaction.ServiceBusClient, Is.Null);
            Assert.That(azureServiceBusTransaction.IncomingQueuePartitionKey, Is.Null);
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

            var transportTransaction = new TransportTransaction();
            var azureServiceBusTransaction = new AzureServiceBusTransaction(transportTransaction, useCrossEntityTransactions: false);
            transportTransaction.Set(azureServiceBusTransaction);

            await dispatcher.Dispatch(new TransportOperations(operation1), transportTransaction);

            var defaultSender = defaultClient.Senders["SomeDestination"];

            Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = defaultSender[defaultSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == default));
            Assert.That(transactionalSender.BatchSentMessages, Is.Empty);
            Assert.That(azureServiceBusTransaction.CommittableTransaction, Is.Null);
        }

        /// <summary>
        /// This test verifies that azure function integration is not accidentally broken
        /// </summary>
        [Test]
        public async Task Should_use_existing_connection_information_from_transport_transaction_when_service_bus_transaction_not_available()
        {
            var client = new FakeServiceBusClient();
            var sender = new FakeSender();
            client.Senders["SomeDestination"] = sender;
            var anotherServiceBusClient = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(new MessageSenderRegistry(client), "sometopic");

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string>(),
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    new DispatchProperties(),
                    DispatchConsistency.Default);

            var transportTransaction = new TransportTransaction();
            transportTransaction.Set<ServiceBusClient>(anotherServiceBusClient);
            transportTransaction.Set("IncomingQueue.PartitionKey", "SomePartitionKey");
            var committableTransaction = new CommittableTransaction();
            transportTransaction.Set(committableTransaction);

            await dispatcher.Dispatch(new TransportOperations(operation1), transportTransaction);

            var someDestinationSenderOnClient = client.Senders["SomeDestination"];
            var someDestinationSenderOnAnotherClient = anotherServiceBusClient.Senders["SomeDestination"];
            var serviceBusTransaction = transportTransaction.Get<AzureServiceBusTransaction>();

            Assert.That(someDestinationSenderOnAnotherClient.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = someDestinationSenderOnAnotherClient[someDestinationSenderOnAnotherClient.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Exactly(1)
                .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == "SomePartitionKey"));
            Assert.That(someDestinationSenderOnClient.BatchSentMessages, Is.Empty);
            Assert.That(serviceBusTransaction.ServiceBusClient, Is.EqualTo(anotherServiceBusClient));
            Assert.That(serviceBusTransaction.IncomingQueuePartitionKey, Is.EqualTo("SomePartitionKey"));
            Assert.That(serviceBusTransaction.CommittableTransaction, Is.EqualTo(committableTransaction));
        }

        class SomeEvent { }
        class SomeOtherEvent { }
    }
}