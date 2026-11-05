namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Logging;
using NServiceBus.Testing;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Routing;

[TestFixture]
public class MissingTopicCreatorTests
{
    [Test]
    public async Task Should_skip_publishes_while_creating_and_create_once()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        for (var i = 0; i < 5; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }
        fake.Gate.SetResult();
        await WaitUntil(() => DispatcherEntries(logOutput).Any(e => e.Level == "INFO"));

        var entries = DispatcherEntries(logOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(1));
            Assert.That(fake.Calls, Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN"), Is.Zero);
            Assert.That(entries.Count(e => e.Level == "INFO" && e.Message.Contains("Created topic sometopic")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_skip_publishes_during_grace_period_after_creating_topic()
    {
        var time = new ManualTimeProvider();
        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true, TimeSpan.FromSeconds(15), time);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => creator.IsInGracePeriod("sometopic"));
        for (var i = 0; i < 3; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }
        time.Advance(TimeSpan.FromSeconds(14));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(1));
            Assert.That(fake.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_create_topic_again_when_it_is_missing_after_grace_period()
    {
        var time = new ManualTimeProvider();
        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true, TimeSpan.FromSeconds(15), time);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => creator.IsInGracePeriod("sometopic"));
        time.Advance(TimeSpan.FromSeconds(16));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => fake.Calls == 2);

        Assert.That(sender.Attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_treat_already_existing_topic_as_created()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation { Failure = new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityAlreadyExists) };
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => creator.IsInGracePeriod("sometopic"));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(1));
            Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN"), Is.Zero);
        }
    }

    [Test]
    public async Task Should_warn_once_per_topic_when_creation_failed()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation { Failure = new UnauthorizedAccessException("No Manage rights") };
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await WaitUntil(() => DispatcherEntries(logOutput).Any(e => e.Message.Contains("Creating topic sometopic")));
        for (var i = 0; i < 3; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }

        var entries = DispatcherEntries(logOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fake.Calls, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.EqualTo(4));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("Creating topic sometopic")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("does not exist")), Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_warn_once_per_topic_without_creating_when_disabled(bool sendIndividually)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: false);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);
        sender.TryAdd = _ => !sendIndividually;

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(typeof(SomeOtherEvent)), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(typeof(SomeOtherEvent)), new TransportTransaction());

        var entries = DispatcherEntries(logOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fake.Calls, Is.Zero);
            Assert.That(sender.Attempts, Is.EqualTo(4), "Only the warning is deduplicated; each publish is still attempted");
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic sometopic ")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic othertopic ")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_not_create_topic_when_throwing_on_missing_topic()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, _) = CreateDispatcherWithMissingTopic(creator, throwOnMissingTopic: true);

        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());
        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fake.Calls, Is.Zero);
            Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN" && e.Message.Contains("does not exist")), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Should_warn_again_for_same_topic_in_new_endpoint_run()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: false);
        var (firstDispatcher, _) = CreateDispatcherWithMissingTopic(creator);
        var (secondDispatcher, _) = CreateDispatcherWithMissingTopic(creator);

        await firstDispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await firstDispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN"), Is.EqualTo(1));

        await secondDispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await secondDispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN"), Is.EqualTo(2));
    }

    [Test]
    public async Task Should_cancel_pending_creation_and_stop_creating_when_disposed()
    {
        var fake = new FakeTopicCreation { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var creator = new MissingTopicCreator(fake.Create, enabled: true);
        creator.TryCreate("sometopic");
        await WaitUntil(() => fake.Calls == 1);

        await creator.DisposeAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fake.Cancelled, Is.EqualTo(1));
            Assert.That(creator.TryCreate("othertopic"), Is.False);
        }
    }

    static (MessageDispatcher Dispatcher, MissingTopicSender Sender) CreateDispatcherWithMissingTopic(MissingTopicCreator creator, bool throwOnMissingTopic = false)
    {
        var client = new FakeServiceBusClient();
        var sender = new MissingTopicSender();
        client.Senders["sometopic"] = sender;
        client.Senders["othertopic"] = sender;

        var dispatcher = new MessageDispatcher(
            client,
            new MessageSenderRegistry(),
            TopicTopology.FromOptions(new TopologyOptions
            {
                PublishedEventToTopicsMap =
                {
                    { typeof(SomeEvent).FullName, "sometopic" },
                    { typeof(SomeOtherEvent).FullName, "othertopic" }
                }
            }),
            new DestinationManager(HierarchyNamespaceOptions.None),
            throwOnMissingTopic,
            creator);

        return (dispatcher, sender);
    }

    static TransportOperations PublishOperations(Type eventType = null) =>
        new(new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(eventType ?? typeof(SomeEvent)),
            [],
            DispatchConsistency.Default));

    // TestingLoggerFactory writes "<timestamp> <LEVEL> <logger name> <message>" per entry, without a separator between entries
    static (string Level, string Message)[] DispatcherEntries(StringWriter logOutput) =>
        [.. LogEntry.Matches(logOutput.ToString())
            .Where(m => m.Groups["logger"].Value == DispatcherLoggerName)
            .Select(m => (m.Groups["level"].Value, m.Groups["message"].Value))];

    static readonly Regex LogEntry = new(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (?<level>[A-Z]+)\s+(?<logger>\S+) (?<message>.*?)(?=\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} |\z)", RegexOptions.Singleline);

    static async Task WaitUntil(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.That(timeout.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)), "Condition not met in time");
            await Task.Delay(5);
        }
    }

    const string DispatcherLoggerName = "NServiceBus.Transport.AzureServiceBus.MessageDispatcher";

    class FakeTopicCreation
    {
        int calls;
        int completed;
        int cancelled;

        public int Calls => Volatile.Read(ref calls);

        public int Completed => Volatile.Read(ref completed);

        public int Cancelled => Volatile.Read(ref cancelled);

        public TaskCompletionSource Gate { get; init; }

        public Exception Failure { get; init; }

        public Func<string, CancellationToken, Task> Create => (_, cancellationToken) => CreateCore(cancellationToken);

        async Task CreateCore(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            try
            {
                if (Gate is not null)
                {
                    await Gate.Task.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref cancelled);
                throw;
            }

            Interlocked.Increment(ref completed);
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    class ManualTimeProvider : TimeProvider
    {
        DateTimeOffset now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    class MissingTopicSender : FakeSender
    {
        int attempts;

        public MissingTopicSender()
        {
            SendMessageBatchAction = _ => FailSend();
            SendMessageAction = _ => FailSend();
        }

        void FailSend()
        {
            Interlocked.Increment(ref attempts);
            throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound);
        }

        public int Attempts => Volatile.Read(ref attempts);
    }

    class SomeEvent;

    class SomeOtherEvent;
}