namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using EventRouting;
    using NServiceBus.Transport.AzureServiceBus.EventRouting;
    using NUnit.Framework;
    using Routing;

    [TestFixture]
    public class MessageDispatcherTests
    {
        [Test]
        public async Task Should_dispatch_unicast_isolated_dispatches_individually()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["SomeDestination"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
                Assert.That(sender.BatchSentMessages, Is.Empty);
            });
        }

        [Test]
        public void Should_rethrow_when_unicast_dispatch_destination_not_available()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var sender = new FakeSender
            {
                SendMessageAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound)
            };
            client.Senders["SomeDestination"] = sender;

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            Assert.That(async () => await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());
        }

        [Test]
        public async Task Should_dispatch_multicast_isolated_dispatches_individually()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } }
                }));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
                Assert.That(sender.BatchSentMessages, Is.Empty);
            });
        }

        // With pub sub the cases of the topic not being available are similar to having a topic without any subscribers
        [Test]
        public void Should_swallow_when_multicast_dispatch_destination_not_available()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()
            {
                PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } }
            }));

            var sender = new FakeSender
            {
                SendMessageAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound),
                SendMessageBatchAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound)
            };
            client.Senders["sometopic"] = sender;

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            Assert.That(async () => await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction()), Throws.Nothing);
        }

        // With pub sub the cases of the topic not being available are similar to having a topic without any subscribers
        [Test]
        public void Should_throw_when_multicast_dispatch_destination_not_available_and_throw_on_missing_topic_enabled()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()
            {
                PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } }
            }), throwOnMissingTopic: true);

            var sender = new FakeSender
            {
                SendMessageAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound),
                SendMessageBatchAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound)
            };
            client.Senders["sometopic"] = sender;

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            Assert.That(async () => await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction()), Throws.Exception.InstanceOf<ServiceBusException>());
        }

        [Test]
        public async Task Should_dispatch_unicast_isolated_dispatches_individually_per_destination()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    [],
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];

            Assert.Multiple(() =>
            {
                Assert.That(someDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(someDestinationSender.BatchSentMessages, Is.Empty);
                Assert.That(someOtherDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(someOtherDestinationSender.BatchSentMessages, Is.Empty);
            });
        }

        [Test]
        public async Task
            Should_dispatch_multicast_isolated_dispatches_individually_regardless_of_the_event_to_the_topic()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap =
                    {
                        { typeof(SomeEvent).FullName, "sometopic" },
                        { typeof(SomeOtherEvent).FullName, "sometopic" }
                    },
                }));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    [],
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
                Assert.That(sender.BatchSentMessages, Is.Empty);
            });
        }

        [Test]
        public async Task Should_dispatch_unicast_default_dispatches_together_as_batch()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["SomeDestination"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Is.Empty);
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            Assert.That(batchContent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Should_dispatch_multicast_default_dispatches_together_as_batch()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } }
                }));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Is.Empty);
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            Assert.That(batchContent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Should_dispatch_unicast_default_dispatches_together_as_batch_per_destination()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];

            Assert.Multiple(() =>
            {
                Assert.That(someDestinationSender.IndividuallySentMessages, Is.Empty);
                Assert.That(someDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someDestinationBatchContent =
                someDestinationSender[someDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.Multiple(() =>
            {
                Assert.That(someDestinationBatchContent, Has.Count.EqualTo(1));

                Assert.That(someOtherDestinationSender.IndividuallySentMessages, Is.Empty);
                Assert.That(someOtherDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someOtherDestinationBatchContent =
                someOtherDestinationSender[someOtherDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherDestinationBatchContent, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task
            Should_dispatch_multicast_default_dispatches_together_as_batch_per_destination()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap =
                    {
                        { typeof(SomeEvent).FullName, "sometopic" },
                        { typeof(SomeOtherEvent).FullName, "someothertopic" }
                    },
                }));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2), new TransportTransaction());

            var someTopicSender = client.Senders["sometopic"];
            var someOtherTopicSender = client.Senders["someothertopic"];

            Assert.Multiple(() =>
            {
                Assert.That(someTopicSender.IndividuallySentMessages, Is.Empty);
                Assert.That(someTopicSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someTopicBatchContent =
                someTopicSender[someTopicSender.BatchSentMessages.ElementAt(0)];
            Assert.Multiple(() =>
            {
                Assert.That(someTopicBatchContent, Has.Count.EqualTo(1));

                Assert.That(someOtherTopicSender.IndividuallySentMessages, Is.Empty);
                Assert.That(someOtherTopicSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someOtherTopicBatchContent =
                someOtherTopicSender[someOtherTopicSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherTopicBatchContent, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Should_allow_mixing_operations()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap =
                    {
                        { typeof(SomeEvent).FullName, "sometopic" },
                        { typeof(SomeOtherEvent).FullName, "someothertopic" }
                    },
                }));

            var operation1 =
                new TransportOperation(new OutgoingMessage("Operation1",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("Operation2",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation3 =
                new TransportOperation(new OutgoingMessage("Operation3",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeOtherDestination"),
                    [],
                    DispatchConsistency.Default);

            var operation4 =
                new TransportOperation(new OutgoingMessage("Operation4",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, operation3, operation4),
                new TransportTransaction());

            var someDestinationSender = client.Senders["SomeDestination"];
            var someOtherDestinationSender = client.Senders["SomeOtherDestination"];
            var someTopicSender = client.Senders["sometopic"];
            var someOtherTopicSender = client.Senders["someothertopic"];

            Assert.Multiple(() =>
            {
                Assert.That(someDestinationSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(someDestinationSender.BatchSentMessages, Is.Empty);

                Assert.That(someOtherDestinationSender.IndividuallySentMessages, Is.Empty);
                Assert.That(someOtherDestinationSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someOtherDestinationBatchContent =
                someOtherDestinationSender[someOtherDestinationSender.BatchSentMessages.ElementAt(0)];
            Assert.Multiple(() =>
            {
                Assert.That(someOtherDestinationBatchContent, Has.Count.EqualTo(1));

                Assert.That(someTopicSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(someTopicSender.BatchSentMessages, Has.Count.Zero);

                Assert.That(someOtherTopicSender.IndividuallySentMessages, Has.Count.Zero);
                Assert.That(someOtherTopicSender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var someOtherTopicSenderBatchContent = someOtherTopicSender[someOtherTopicSender.BatchSentMessages.ElementAt(0)];
            Assert.That(someOtherTopicSenderBatchContent, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Should_use_connection_information_of_existing_service_bus_transaction()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;
            var transactionalClient = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction(transactionalClient,
                "SomePartitionKey", new TransactionOptions());

            await dispatcher.Dispatch(new TransportOperations(operation1),
                azureServiceBusTransaction.TransportTransaction);

            var transactionalSender = transactionalClient.Senders["SomeDestination"];

            Assert.That(transactionalSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someDestinationTransactionalBatchContent =
                transactionalSender[transactionalSender.BatchSentMessages.ElementAt(0)];
            Assert.Multiple(() =>
            {
                Assert.That(someDestinationTransactionalBatchContent, Has.Exactly(1)
                    .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == "SomePartitionKey"));
                Assert.That(defaultSender.BatchSentMessages, Is.Empty);
                Assert.That(azureServiceBusTransaction.Transaction, Is.Not.Null);
            });
        }

        [Test]
        public void Should_throw_when_detecting_more_than_hundred_messages_when_transactions_used()
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

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var nrOfMessages = 150;
            var operations = new List<TransportOperation>(nrOfMessages);
            for (int i = 0; i < nrOfMessages; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction(transactionalClient,
                "SomePartitionKey", new TransactionOptions());

            var ex = Assert.ThrowsAsync<Exception>(async () =>
                await dispatcher.Dispatch(new TransportOperations(operations.ToArray()),
                    azureServiceBusTransaction.TransportTransaction));
            Assert.That(ex.Message,
                Is.EqualTo(
                    $"The number of outgoing messages ({nrOfMessages}) exceeds the limits permitted by Azure Service Bus ({100}) in a single transaction"));
        }

        [Test]
        public async Task Should_split_into_multiple_batches_according_to_the_sdk()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;

            bool firstTime = true;
            defaultSender.TryAdd = msg =>
            {
                if ((string)msg.ApplicationProperties["Number"] != "150" || !firstTime)
                {
                    return true;
                }

                firstTime = false;
                return false;
            };

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operations = new List<TransportOperation>(200);
            for (int i = 0; i < 200; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            await dispatcher.Dispatch(new TransportOperations([.. operations]),
                azureServiceBusTransaction.TransportTransaction);

            Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(2));
            var firstBatch = defaultSender[defaultSender.BatchSentMessages.ElementAt(0)];
            var secondBatch = defaultSender[defaultSender.BatchSentMessages.ElementAt(1)];
            Assert.Multiple(() =>
            {
                Assert.That(firstBatch, Has.Count.EqualTo(150));
                Assert.That(secondBatch, Has.Count.EqualTo(50));
            });
        }

        [Test]
        public async Task Should_fallback_to_individual_sends_when_messages_cannot_be_added_to_batch()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;

            defaultSender.TryAdd = msg => false;

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operations = new List<TransportOperation>(5);
            for (int i = 0; i < 5; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            await dispatcher.Dispatch(new TransportOperations([.. operations]),
                azureServiceBusTransaction.TransportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(defaultSender.BatchSentMessages, Has.Count.Zero);
                Assert.That(defaultSender.IndividuallySentMessages, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public async Task Should_swallow_when_multicast_dispatch_sent_as_single_messages_destination_not_available()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender
            {
                SendMessageAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound),
                SendMessageBatchAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound)
            };
            defaultClient.Senders[typeof(SomeEvent).FullName] = defaultSender;

            defaultSender.TryAdd = msg => false;

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operations = new List<TransportOperation>(5);
            for (int i = 0; i < 5; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            Assert.Multiple(() =>
            {
                Assert.That(async () => await dispatcher.Dispatch(new TransportOperations([.. operations]), azureServiceBusTransaction.TransportTransaction), Throws.Nothing);
                Assert.That(defaultSender.BatchSentMessages, Has.Count.Zero);
                Assert.That(defaultSender.IndividuallySentMessages, Has.Count.EqualTo(0));
            });
        }

        [Test]
        public async Task Should_throw_when_multicast_dispatch_sent_as_single_messages_destination_not_available_and_throw_on_exception_set()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender
            {
                SendMessageAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound),
                SendMessageBatchAction = _ => throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound)
            };
            defaultClient.Senders[typeof(SomeEvent).FullName] = defaultSender;

            defaultSender.TryAdd = msg => false;

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()), throwOnMissingTopic: true);

            var operations = new List<TransportOperation>(5);
            for (int i = 0; i < 5; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            Assert.Multiple(() =>
            {
                Assert.That(async () => await dispatcher.Dispatch(new TransportOperations([.. operations]), azureServiceBusTransaction.TransportTransaction), Throws.Exception.InstanceOf<ServiceBusException>());
                Assert.That(defaultSender.BatchSentMessages, Has.Count.Zero);
                Assert.That(defaultSender.IndividuallySentMessages, Has.Count.EqualTo(0));
            });
        }

        [Test]
        public async Task
            Should_fallback_to_individual_send_when_a_message_cannot_be_added_to_a_batch_but_batch_all_others()
        {
            var defaultClient = new FakeServiceBusClient();
            var defaultSender = new FakeSender();
            defaultClient.Senders["SomeDestination"] = defaultSender;

            defaultSender.TryAdd = msg =>
            {
                return (string)msg.ApplicationProperties["Number"] switch
                {
                    "4" or "7" => false,
                    _ => true,
                };
            };

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operations = new List<TransportOperation>(5);
            for (int i = 0; i < 10; i++)
            {
                operations.Add(new TransportOperation(new OutgoingMessage($"SomeId{i}",
                        new Dictionary<string, string> { { "Number", i.ToString() } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default));
            }

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            await dispatcher.Dispatch(new TransportOperations(operations.ToArray()),
                azureServiceBusTransaction.TransportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(3));
                Assert.That(defaultSender.IndividuallySentMessages, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task Should_use_default_connection_information_when_existing_service_bus_transaction_has_none()
        {
            var defaultClient = new FakeServiceBusClient();
            var transactionalClient = new FakeServiceBusClient();
            var transactionalSender = new FakeSender();
            transactionalClient.Senders["SomeDestination"] = transactionalSender;

            var dispatcher = new MessageDispatcher(defaultClient, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()));

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var azureServiceBusTransaction = new AzureServiceBusTransportTransaction();

            await dispatcher.Dispatch(new TransportOperations(operation1),
                azureServiceBusTransaction.TransportTransaction);

            var defaultSender = defaultClient.Senders["SomeDestination"];

            Assert.That(defaultSender.BatchSentMessages, Has.Count.EqualTo(1));
            var someOtherDestinationBatchContent = defaultSender[defaultSender.BatchSentMessages.ElementAt(0)];
            Assert.Multiple(() =>
            {
                Assert.That(someOtherDestinationBatchContent, Has.Exactly(1)
                    .Matches<ServiceBusMessage>(msg => msg.TransactionPartitionKey == null));
                Assert.That(transactionalSender.BatchSentMessages, Is.Empty);
                Assert.That(azureServiceBusTransaction.Transaction, Is.Null);
            });
        }

        [Test]
        public async Task Should_respect_hierarchy_namespace_on_unicast_dispatch()
        {
            var client = new FakeServiceBusClient();
            var destinationManager = GetDestinationManager("SomeHierarchyNamespace");

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()), destinationManager, throwOnMissingTopic: false);

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var batchOperation1 =
                new TransportOperation(new OutgoingMessage("SomeBatchId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var batchOperation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherBatchId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, batchOperation1, batchOperation2), new TransportTransaction());

            var sender = client.Senders[destinationManager.GetDestination("SomeDestination")];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_respect_hierarchy_namespace_on_multicast_dispatch()
        {
            var client = new FakeServiceBusClient();
            var destinationManager = GetDestinationManager("SomeHierarchyNamespace");

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } }
                }),
                destinationManager, throwOnMissingTopic: false);

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var batchOperation1 =
                new TransportOperation(new OutgoingMessage("SomeBatchId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            var batchOperation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherBatchId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, batchOperation1, batchOperation2), new TransportTransaction());

            var sender = client.Senders[destinationManager.GetDestination("sometopic")];

            Assert.Multiple(() =>
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(2));
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_exclude_configured_message_types_from_hierarchy_namespace_on_unicast_dispatch()
        {
            var client = new FakeServiceBusClient();
            var destinationManager = GetDestinationManager("SomeHierarchyNamespace", options =>
            {
                options.ExcludeMessageType<SomeOtherCommand>();
                options.ExcludeMessageType<ISomeCommandInterface>();
            });

            var dispatcher = new MessageDispatcher(client, new MessageSenderRegistry(), TopicTopology.FromOptions(new TopologyOptions()), destinationManager, throwOnMissingTopic: false);

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeCommand).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeOtherCommand).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Isolated);

            var batchOperation1 =
                new TransportOperation(new OutgoingMessage("SomeBatchId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeCommand).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            var batchOperation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherBatchId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, $"{typeof(SomeImplementedCommand).FullName};{typeof(ISomeCommandInterface).FullName}" } },
                        ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag("SomeDestination"),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, batchOperation1, batchOperation2), new TransportTransaction());

            var externalSender = client.Senders["SomeDestination"];
            var hierarchySender = client.Senders[destinationManager.GetDestination("SomeDestination")];

            Assert.Multiple(() =>
            {
                Assert.That(externalSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(externalSender.BatchSentMessages, Has.Count.EqualTo(1));
                Assert.That(hierarchySender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(hierarchySender.BatchSentMessages, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_exclude_configured_message_types_from_hierarchy_namespace_on_multicast_dispatch()
        {
            var client = new FakeServiceBusClient();
            var destinationManager = GetDestinationManager("SomeHierarchyNamespace", options =>
            {
                options.ExcludeMessageType<SomeOtherEvent>();
                options.ExcludeMessageType<ISomeEventInterface>();
            });

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" }, { typeof(SomeOtherEvent).FullName, "sometopic" }, { typeof(SomeImplementedEvent).FullName, "sometopic" } }
                }),
                destinationManager, throwOnMissingTopic: false);

            var operation1 =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var operation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeOtherEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeOtherEvent)),
                    [],
                    DispatchConsistency.Isolated);

            var batchOperation1 =
                new TransportOperation(new OutgoingMessage("SomeBatchId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            var batchOperation2 =
                new TransportOperation(new OutgoingMessage("SomeOtherBatchId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, $"{typeof(SomeImplementedEvent).FullName};{typeof(ISomeEventInterface).FullName}" } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeImplementedEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation1, operation2, batchOperation1, batchOperation2), new TransportTransaction());

            var externalSender = client.Senders["sometopic"];
            var hierarchySender = client.Senders[destinationManager.GetDestination("sometopic")];

            Assert.Multiple(() =>
            {
                Assert.That(externalSender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(externalSender.BatchSentMessages, Has.Count.EqualTo(1));
                Assert.That(hierarchySender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(hierarchySender.BatchSentMessages, Has.Count.EqualTo(1));
            });
        }

        DestinationManager GetDestinationManager(string hierarchyNamespace = null, Action<HierarchyNamespaceOptions> configureHierarchyNamespaceOptions = null)
        {
            if (hierarchyNamespace == null)
            {
                return new DestinationManager(null);
            }

            var options = new HierarchyNamespaceOptions { HierarchyNamespace = hierarchyNamespace };
            configureHierarchyNamespaceOptions?.Invoke(options);

            return new DestinationManager(options);
        }

        class SomeEvent;

        class SomeOtherEvent;

        class SomeImplementedEvent : ISomeEventInterface;

        interface ISomeEventInterface;

        class SomeCommand;

        class SomeOtherCommand;

        class SomeImplementedCommand : ISomeCommandInterface;

        interface ISomeCommandInterface;

        [Test]
        public async Task Should_apply_correlation_property_stamps_for_correlation_routed_events()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            Assert.That(batchContent, Has.Count.EqualTo(1));
            var message = batchContent.ElementAt(0);
            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
                Assert.That(message.ApplicationProperties[typeof(SomeEvent).FullName], Is.EqualTo(true));
            });
        }

        [Test]
        public async Task Should_not_apply_correlation_stamps_when_routing_is_not_multiplexed()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.NotMultiplexed } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);
            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties.ContainsKey(typeof(SomeEvent).FullName), Is.False);
            });
        }

        [Test]
        public async Task Should_not_apply_correlation_stamps_for_sql_filter_mode()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.SqlFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).FullName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            Assert.Multiple(() =>
            {
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            });
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);
            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties.ContainsKey(typeof(SomeEvent).FullName), Is.False);
            });
        }

        [Test]
        public async Task Should_handle_multiple_message_types_in_enclosed_header()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, $"{typeof(SomeEvent).FullName};{typeof(SomeOtherEvent).FullName}" } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];

            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);
            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeOtherEvent).FullName));
            });
        }

        [Test]
        public async Task Should_fallback_to_concrete_event_type_when_enclosed_message_types_header_is_missing()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
                Assert.That(message.ApplicationProperties[typeof(SomeEvent).FullName], Is.EqualTo(true));
            });
        }

        [Test]
        public async Task Should_apply_fallback_topic_correlation_routing_for_unmapped_events()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    FallbackTopic = new FallbackTopicOptions { TopicName = "sharedtopic", Mode = TopicRoutingMode.CorrelationFilter }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sharedtopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
        }

        [Test]
        public async Task Should_route_unmapped_events_to_sql_filter_fallback_topic_without_correlation_stamps()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    FallbackTopic = new FallbackTopicOptions { TopicName = "sharedtopic", Mode = TopicRoutingMode.SqlFilter }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sharedtopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.That(message.ApplicationProperties.ContainsKey(typeof(SomeEvent).FullName), Is.False);
        }

        [Test]
        public async Task Should_not_apply_fallback_topic_correlation_routing_to_explicitly_mapped_events_without_routing_override()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    FallbackTopic = new FallbackTopicOptions { TopicName = "sharedtopic", Mode = TopicRoutingMode.CorrelationFilter }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.That(message.ApplicationProperties.ContainsKey(typeof(SomeEvent).FullName), Is.False);
        }

        [Test]
        public async Task Should_apply_correlation_property_stamps_for_isolated_multicast_dispatches()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        [],
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Isolated);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];
            var message = sender.IndividuallySentMessages.Single();

            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
                Assert.That(message.ApplicationProperties[typeof(SomeEvent).FullName], Is.EqualTo(true));
            });
        }

        [Test]
        public async Task Should_normalize_assembly_qualified_enclosed_message_types_for_correlation_stamps()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, typeof(SomeEvent).AssemblyQualifiedName } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(SomeEvent).FullName));
                Assert.That(message.ApplicationProperties.ContainsKey(typeof(SomeEvent).AssemblyQualifiedName), Is.False);
            });
        }

        [Test]
        public async Task Should_normalize_proxy_style_enclosed_message_types_for_correlation_stamps()
        {
            var client = new FakeServiceBusClient();

            var dispatcher = new MessageDispatcher(
                client,
                new MessageSenderRegistry(),
                TopicTopology.FromOptions(new TopologyOptions
                {
                    PublishedEventToTopicsMap = { { typeof(SomeImplementedEvent).FullName, "sometopic" } },
                    RoutingOptionsMap = { { typeof(SomeImplementedEvent).FullName, new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter } } }
                }));

            var proxyType = $"{typeof(SomeImplementedEvent).FullName}__impl";
            var assemblyName = typeof(SomeImplementedEvent).Assembly.GetName().Name;
            var enclosedMessageTypes = $"{proxyType}, {assemblyName};{typeof(ISomeEventInterface).AssemblyQualifiedName}";

            var operation =
                new TransportOperation(new OutgoingMessage("SomeId",
                        new Dictionary<string, string> { { Headers.EnclosedMessageTypes, enclosedMessageTypes } },
                        ReadOnlyMemory<byte>.Empty),
                    new MulticastAddressTag(typeof(SomeImplementedEvent)),
                    [],
                    DispatchConsistency.Default);

            await dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction());

            var sender = client.Senders["sometopic"];
            var batchContent = sender[sender.BatchSentMessages.ElementAt(0)];
            var message = batchContent.ElementAt(0);

            Assert.Multiple(() =>
            {
                Assert.That(message.ApplicationProperties, Contains.Key(typeof(ISomeEventInterface).FullName));
                Assert.That(message.ApplicationProperties.ContainsKey(proxyType), Is.False);
                Assert.That(message.ApplicationProperties.ContainsKey(typeof(ISomeEventInterface).AssemblyQualifiedName), Is.False);
            });
        }
    }
}
