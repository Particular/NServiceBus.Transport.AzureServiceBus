namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
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
public class MissingTopicLoggingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_warn_once_per_topic_when_a_missing_topic_is_swallowed(bool sendIndividually)
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender { TryAdd = _ => !sendIndividually };
        var dispatcher = CreateDispatcher(sender, throwOnMissingTopic: false);

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventB)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventB)), new TransportTransaction());

        var entries = DispatcherEntries(output);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.Attempts, Is.EqualTo(4), "Only the warning is deduplicated; each publish is still attempted");
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic topic-a ")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic topic-b ")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_warn_again_for_the_same_topic_in_a_new_endpoint_run()
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var first = CreateDispatcher(new MissingTopicSender(), throwOnMissingTopic: false);
        var second = CreateDispatcher(new MissingTopicSender(), throwOnMissingTopic: false);

        await first.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await first.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        Assert.That(DispatcherEntries(output).Count(e => e.Level == "WARN"), Is.EqualTo(1));

        await second.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await second.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        Assert.That(DispatcherEntries(output).Count(e => e.Level == "WARN"), Is.EqualTo(2));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Should_throw_without_logging_when_throwing_on_missing_topics(int failurePath)
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender
        {
            TryAdd = _ => failurePath != 1,
            BatchCreationFails = failurePath == 2
        };
        var dispatcher = CreateDispatcher(sender, throwOnMissingTopic: true);

        var first = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        var second = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(second.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(DispatcherEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    static MessageDispatcher CreateDispatcher(MissingTopicSender sender, bool throwOnMissingTopic)
    {
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = sender;
        client.Senders["topic-b"] = sender;
        return new MessageDispatcher(client, new MessageSenderRegistry(),
            TopicTopology.FromOptions(new TopologyOptions
            {
                PublishedEventToTopicsMap =
                {
                    { typeof(EventA).FullName, "topic-a" },
                    { typeof(EventB).FullName, "topic-b" }
                }
            }),
            new DestinationManager(HierarchyNamespaceOptions.None), throwOnMissingTopic);
    }

    static TransportOperations Publish(Type eventType) =>
        new(new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(eventType), [], DispatchConsistency.Default));

    static StringWriter CaptureLogs(out IDisposable scope)
    {
        var output = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        scope = LogManager.Use<TestingLoggerFactory>().BeginScope(output, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete
        return output;
    }

    // TestingLoggerFactory writes "<timestamp> <LEVEL> <logger name> <message>" per entry, without a separator between entries
    static (string Level, string Message)[] DispatcherEntries(StringWriter output) =>
        [.. LogEntry.Matches(output.ToString())
            .Where(m => m.Groups["logger"].Value == "NServiceBus.Transport.AzureServiceBus.MessageDispatcher")
            .Select(m => (m.Groups["level"].Value, m.Groups["message"].Value))];

    static readonly Regex LogEntry = new(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (?<level>[A-Z]+)\s+(?<logger>\S+) (?<message>.*?)(?=\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} |\z)", RegexOptions.Singleline);

    class MissingTopicSender : FakeSender
    {
        public MissingTopicSender()
        {
            SendMessageBatchAction = _ => FailSend();
            SendMessageAction = _ => FailSend();
        }

        public bool BatchCreationFails { get; init; }

        public int Attempts { get; private set; }

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default) =>
            BatchCreationFails
                ? throw new ServiceBusException("Missing", ServiceBusFailureReason.MessagingEntityNotFound)
                : base.CreateMessageBatchAsync(cancellationToken);

        void FailSend()
        {
            Attempts++;
            throw new ServiceBusException("Missing", ServiceBusFailureReason.MessagingEntityNotFound);
        }
    }

    class EventA;

    class EventB;
}