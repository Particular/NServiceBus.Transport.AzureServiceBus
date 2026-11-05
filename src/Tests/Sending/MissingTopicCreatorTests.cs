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
    public async Task Should_create_missing_topic_once_while_creation_is_in_progress()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(creator);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => dispatcher.Dispatch(PublishOperations(), new TransportTransaction())));
        fake.Gate.SetResult();
        await WaitUntil(() => DispatcherEntries(logOutput).Any(e => e.Level == "INFO"));

        var entries = DispatcherEntries(logOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(5));
            Assert.That(fake.Calls, Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN"), Is.Zero);
            Assert.That(entries.Count(e => e.Level == "INFO" && e.Message.Contains("Created topic sometopic")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_create_topic_again_when_it_goes_missing_after_being_created()
    {
        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, _) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await PublishUntil(dispatcher, () => fake.Calls == 2);

        Assert.That(fake.Calls, Is.EqualTo(2));
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
        var (dispatcher, _) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await PublishUntil(dispatcher, () => fake.Calls == 2);

        Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN"), Is.Zero);
    }

    [Test]
    public async Task Should_warn_once_and_fall_back_to_warning_per_publish_when_creation_fails()
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
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("does not exist")), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Should_not_create_topic_when_disabled()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: false);
        var (dispatcher, _) = CreateDispatcherWithMissingTopic(creator);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fake.Calls, Is.Zero);
            Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN" && e.Message.Contains("does not exist")), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Should_not_create_topic_when_throwing_on_missing_topic()
    {
        var fake = new FakeTopicCreation();
        await using var creator = new MissingTopicCreator(fake.Create, enabled: true);
        var (dispatcher, _) = CreateDispatcherWithMissingTopic(creator, throwOnMissingTopic: true);

        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());
        Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());

        Assert.That(fake.Calls, Is.Zero);
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

        var dispatcher = new MessageDispatcher(
            client,
            new MessageSenderRegistry(),
            TopicTopology.FromOptions(new TopologyOptions { PublishedEventToTopicsMap = { { typeof(SomeEvent).FullName, "sometopic" } } }),
            new DestinationManager(HierarchyNamespaceOptions.None),
            throwOnMissingTopic,
            creator);

        return (dispatcher, sender);
    }

    static TransportOperations PublishOperations() =>
        new(new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(typeof(SomeEvent)),
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

    // Publishes that find the topic missing while its creation is still running don't start another one
    static async Task PublishUntil(MessageDispatcher dispatcher, Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.That(timeout.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)), "Condition not met in time");
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
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

    class MissingTopicSender : FakeSender
    {
        int attempts;

        public MissingTopicSender() =>
            SendMessageBatchAction = _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new ServiceBusException("Some exception", ServiceBusFailureReason.MessagingEntityNotFound);
            };

        public int Attempts => Volatile.Read(ref attempts);
    }

    class SomeEvent;
}