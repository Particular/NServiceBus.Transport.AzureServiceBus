namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Routing;

[TestFixture]
public class MissingTopicTrackerTests
{
    [Test]
    public async Task Should_skip_publishes_to_missing_topic_until_probe_finds_it()
    {
        var probe = new FakeProbe();
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromHours(1));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker);

        for (var i = 0; i < 5; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }

        Assert.That(sender.Attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_resume_publishing_once_probe_finds_topic()
    {
        var probe = new FakeProbe();
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => probe.Calls >= 3);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracker.IsMissing("sometopic"), Is.True);
            Assert.That(sender.Attempts, Is.EqualTo(1));
        }

        probe.TopicExists = true;
        sender.TopicExists = true;
        await WaitUntil(() => !tracker.IsMissing("sometopic"));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_keep_probing_when_probe_fails()
    {
        var probe = new FakeProbe { Throw = true };
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        tracker.MarkMissing("sometopic");

        await WaitUntil(() => probe.Calls >= 3);

        Assert.That(tracker.IsMissing("sometopic"), Is.True);
    }

    [Test]
    public async Task Should_stop_probing_when_disposed()
    {
        var probe = new FakeProbe();
        var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        tracker.MarkMissing("sometopic");
        await WaitUntil(() => probe.Calls >= 1);

        await tracker.DisposeAsync();
        var callsAfterDispose = probe.Calls;
        await Task.Delay(100);

        Assert.That(probe.Calls, Is.EqualTo(callsAfterDispose));
    }

    [Test]
    public async Task Should_not_skip_when_throwing_on_missing_topic()
    {
        var probe = new FakeProbe();
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromHours(1));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker, throwOnMissingTopic: true);

        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());
        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(2));
            Assert.That(tracker.IsMissing("sometopic"), Is.False);
        }
    }

    [Test]
    public async Task Should_attempt_every_publish_without_tracker()
    {
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(null);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        Assert.That(sender.Attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_warn_once_per_topic_until_it_exists()
    {
        await using var tracker = new MissingTopicTracker(new FakeProbe().Check, TimeSpan.FromHours(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracker.MarkMissing("sometopic"), Is.True);
            Assert.That(tracker.MarkMissing("SomeTopic"), Is.False);
            Assert.That(tracker.MarkMissing("othertopic"), Is.True);
            Assert.That(tracker.MarkPresent("sometopic"), Is.True);
            Assert.That(tracker.MarkPresent("sometopic"), Is.False);
            Assert.That(tracker.MarkMissing("sometopic"), Is.True);
        }
    }

    static (MessageDispatcher Dispatcher, MissingTopicSender Sender) CreateDispatcherWithMissingTopic(MissingTopicTracker tracker, bool throwOnMissingTopic = false)
    {
        var client = new FakeServiceBusClient();
        var sender = new MissingTopicSender();
        client.Senders["sometopic"] = sender;

        var dispatcher = new MessageDispatcher(
            client,
            new MessageSenderRegistry(),
            TopicTopology.FromOptions(new TopologyOptions { PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } } }),
            new DestinationManager(HierarchyNamespaceOptions.None),
            throwOnMissingTopic,
            missingTopicTracker: tracker);

        return (dispatcher, sender);
    }

    static TransportOperations PublishOperations() =>
        new(new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(typeof(SomeEvent)),
            [],
            DispatchConsistency.Default));

    static async Task WaitUntil(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.That(timeout.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)), "Condition not met in time");
            await Task.Delay(5);
        }
    }

    class FakeProbe
    {
        int calls;

        public int Calls => Volatile.Read(ref calls);

        public volatile bool TopicExists;

        public volatile bool Throw;

        public Func<string, CancellationToken, Task<bool>> Check => (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Throw
                ? Task.FromException<bool>(new ServiceBusException("Some exception", ServiceBusFailureReason.ServiceTimeout))
                : Task.FromResult(TopicExists);
        };
    }

    class MissingTopicSender : FakeSender
    {
        public MissingTopicSender() =>
            SendMessageBatchAction = _ =>
            {
                Attempts++;
                if (!TopicExists)
                {
                    throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound);
                }
            };

        public int Attempts { get; private set; }

        public bool TopicExists { get; set; }
    }

    class SomeEvent;
}