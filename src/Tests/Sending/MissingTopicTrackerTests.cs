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
    public async Task Should_stop_skipping_and_warn_when_checks_keep_failing()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var probe = new FakeProbe { Throw = true };
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        // The tracker stops skipping first and logs afterwards
        await WaitUntil(() => DispatcherEntries(logOutput).Any(e => e.Message.Contains("failed 3 times in a row")));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        var entries = DispatcherEntries(logOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(probe.Calls, Is.EqualTo(MissingTopicTracker.MaxConsecutiveFailedChecks));
            Assert.That(sender.Attempts, Is.EqualTo(2), "The publish after the checks gave up should be attempted");
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("failed 3 times in a row")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_reset_failed_checks_when_check_says_topic_is_missing()
    {
        var probe = new FakeProbe { ThrowOnCalls = [1, 2, 4, 5] };
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        tracker.MarkMissing("sometopic");

        await WaitUntil(() => probe.Calls >= 6);

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
    public async Task Should_log_one_warning_per_missing_topic_and_one_info_when_it_exists()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var probe = new FakeProbe();
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromMilliseconds(10));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker);

        for (var i = 0; i < 5; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }
        var whileMissing = DispatcherEntries(logOutput);

        probe.TopicExists = true;
        sender.TopicExists = true;
        // The tracker stops skipping first and logs afterwards
        await WaitUntil(() => DispatcherEntries(logOutput).Any(e => e.Level == "INFO"));
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        var afterAppearing = DispatcherEntries(logOutput);

        probe.TopicExists = false;
        sender.TopicExists = false;
        for (var i = 0; i < 5; i++)
        {
            await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        }
        var afterMissingAgain = DispatcherEntries(logOutput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileMissing.Count(e => e.Level == "WARN"), Is.EqualTo(1));
            Assert.That(whileMissing.Count(e => e.Level == "INFO"), Is.Zero);
            Assert.That(afterAppearing.Count(e => e.Level == "WARN"), Is.EqualTo(1));
            Assert.That(afterAppearing.Count(e => e.Level == "INFO" && e.Message.Contains("exists now")), Is.EqualTo(1));
            Assert.That(afterMissingAgain.Count(e => e.Level == "WARN"), Is.EqualTo(2));
            Assert.That(afterMissingAgain.Count(e => e.Level == "INFO"), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_warn_and_throw_on_every_publish_when_throwing_on_missing_topic()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var probe = new FakeProbe();
        await using var tracker = new MissingTopicTracker(probe.Check, TimeSpan.FromHours(1));
        var (dispatcher, sender) = CreateDispatcherWithMissingTopic(tracker, throwOnMissingTopic: true);

        for (var i = 0; i < 3; i++)
        {
            Assert.That(async () => await dispatcher.Dispatch(PublishOperations(), new TransportTransaction()), Throws.InstanceOf<ServiceBusException>());
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(3));
            Assert.That(tracker.IsMissing("sometopic"), Is.False);
            Assert.That(DispatcherEntries(logOutput).Count(e => e.Level == "WARN" && e.Message.Contains("does not exist")), Is.EqualTo(3));
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

    class FakeProbe
    {
        int calls;

        public int Calls => Volatile.Read(ref calls);

        public volatile bool TopicExists;

        public volatile bool Throw;

        public int[] ThrowOnCalls { get; init; } = [];

        public Func<string, CancellationToken, Task<bool>> Check => (_, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            return Throw || ThrowOnCalls.Contains(call)
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