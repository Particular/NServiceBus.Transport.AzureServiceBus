namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
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
    public async Task Should_wait_for_shared_creation_and_deliver_each_pending_publish()
    {
        var sender = new MissingTopicSender();
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        await using var creator = CreateCreator(async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            sender.TopicExists = true;
        });
        var dispatcher = CreateDispatcher(creator, sender);

        var first = dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var others = Enumerable.Range(0, 4).Select(_ => dispatcher.Dispatch(PublishOperations(), new TransportTransaction())).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(others.All(task => !task.IsCompleted), Is.True);
            Assert.That(sender.Attempts, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
        }

        release.SetResult();
        await Task.WhenAll(others.Append(first));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(5));
            Assert.That(sender.Attempts, Is.EqualTo(6));
            Assert.That(calls, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_retry_stale_404s_with_same_messages_after_creation(bool sendIndividually)
    {
        var sender = new MissingTopicSender { FailuresRemaining = 4, TryAdd = _ => !sendIndividually };
        var calls = 0;
        await using var creator = CreateCreator((_, _) =>
        {
            calls++;
            sender.TopicExists = true;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(creator, sender);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(1), "Post-creation 404s must not issue another create call within the same recovery");
            Assert.That(sender.Attempts, Is.EqualTo(5));
            if (sendIndividually)
            {
                Assert.That(sender.IndividuallySentMessages, Has.Count.EqualTo(1));
                Assert.That(sender.AttemptedMessages.All(m => ReferenceEquals(m, sender.AttemptedMessages[0])), Is.True);
            }
            else
            {
                Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
                Assert.That(sender.AttemptedBatches.All(b => ReferenceEquals(b, sender.AttemptedBatches[0])), Is.True);
            }
        }
    }

    [Test]
    public async Task Should_recover_batch_creation_without_losing_queued_messages()
    {
        var sender = new MissingTopicSender { BatchCreationFailures = 3 };
        var calls = 0;
        await using var creator = CreateCreator((_, _) =>
        {
            calls++;
            sender.TopicExists = true;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(creator, sender);

        await dispatcher.Dispatch(PublishOperations(count: 3), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(4));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(sender[sender.BatchSentMessages.Single()], Has.Count.EqualTo(3));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_throw_when_post_creation_delivery_attempts_are_exhausted(bool sendIndividually)
    {
        var sender = new MissingTopicSender { TryAdd = _ => !sendIndividually };
        var calls = 0;
        await using var creator = CreateCreator((_, _) => { calls++; return Task.CompletedTask; });
        var dispatcher = CreateDispatcher(creator, sender);

        var exception = Assert.ThrowsAsync<TimeoutException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.InnerException, Is.InstanceOf<ServiceBusException>());
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.EqualTo(1 + MissingTopicCreator.MaxDeliveryAttemptsAfterCreation));
            Assert.That(sender.BatchSentMessages, Is.Empty);
            Assert.That(sender.IndividuallySentMessages, Is.Empty);
        }
    }

    [Test]
    public async Task Should_bound_the_wait_for_creation()
    {
        var sender = new MissingTopicSender();
        var entered = Signal();
        var release = Signal();
        await using var creator = new MissingTopicCreator(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        }, true, TimeSpan.FromMilliseconds(100), TimeSpan.Zero);
        var dispatcher = CreateDispatcher(creator, sender);

        var publish = dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = Assert.ThrowsAsync<TimeoutException>(async () => await publish.WaitAsync(TimeSpan.FromSeconds(5)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"did not recover within {0.1:0.###} seconds"));
            Assert.That(sender.Attempts, Is.EqualTo(1));
        }
        release.TrySetResult();
    }

    [Test]
    public async Task Should_bound_the_wait_for_an_unresponsive_delivery()
    {
        var blocked = Signal();
        var attempts = 0;
        await using var creator = new MissingTopicCreator(static (_, _) => Task.CompletedTask,
            true, TimeSpan.FromMilliseconds(100), TimeSpan.Zero);

        Task Operation(CancellationToken _) => ++attempts == 1
            ? Task.FromException(MissingTopic())
            : blocked.Task;

        try
        {
            var exception = Assert.ThrowsAsync<TimeoutException>(async () => await creator.ExecuteAsync("sometopic", Operation).WaitAsync(TimeSpan.FromSeconds(5)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception.Message, Does.Contain($"did not recover within {0.1:0.###} seconds"));
                Assert.That(attempts, Is.EqualTo(2), "A timed-out operation must not start another retry");
            }
        }
        finally
        {
            blocked.TrySetResult();
        }
    }

    [Test]
    public async Task Should_cancel_one_waiter_without_cancelling_shared_creation()
    {
        var sender = new MissingTopicSender();
        var entered = Signal();
        var release = Signal();
        await using var creator = CreateCreator(async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            sender.TopicExists = true;
        });
        var dispatcher = CreateDispatcher(creator, sender);
        using var cancellation = new CancellationTokenSource();
        var cancelledPublish = dispatcher.Dispatch(PublishOperations(), new TransportTransaction(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var successfulPublish = dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        await cancellation.CancelAsync();
        Assert.That(async () => await cancelledPublish, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(successfulPublish.IsCompleted, Is.False);
        release.SetResult();
        await successfulPublish;
        Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Should_propagate_creation_failure_and_allow_a_later_publish_to_retry_it()
    {
        var sender = new MissingTopicSender();
        var calls = 0;
        await using var creator = CreateCreator((_, _) =>
        {
            if (++calls == 1)
            {
                return Task.FromException(new UnauthorizedAccessException("No Manage rights"));
            }
            sender.TopicExists = true;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(creator, sender);

        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        Assert.That(failure.InnerException, Is.InstanceOf<UnauthorizedAccessException>());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_not_swallow_a_missing_entity_error_from_provisioning()
    {
        await using var creator = CreateCreator(static (_, _) => Task.FromException(MissingTopic()));
        var dispatcher = CreateDispatcher(creator, new MissingTopicSender());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        Assert.That(exception.InnerException, Is.InstanceOf<ServiceBusException>());
    }

    [Test]
    public async Task Should_attempt_delivery_when_another_participant_created_the_topic()
    {
        var sender = new MissingTopicSender();
        await using var creator = CreateCreator((_, _) =>
        {
            sender.TopicExists = true;
            return Task.FromException(new ServiceBusException("Already created", ServiceBusFailureReason.MessagingEntityAlreadyExists));
        });
        var dispatcher = CreateDispatcher(creator, sender);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());

        Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Should_not_retry_a_different_delivery_failure()
    {
        var sender = new MissingTopicSender();
        await using var creator = CreateCreator((_, _) =>
        {
            sender.TopicExists = true;
            sender.FailureAfterCreation = ServiceBusFailureReason.MessageSizeExceeded;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(creator, sender);

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessageSizeExceeded));
            Assert.That(sender.Attempts, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Should_keep_transactional_retries_on_the_receivers_client()
    {
        var receiverClient = new FakeServiceBusClient();
        var defaultClient = new FakeServiceBusClient();
        defaultClient.Senders["sometopic"] = new MissingTopicSender();
        var sender = new MissingTopicSender { FailuresRemaining = 2 };
        receiverClient.Senders["sometopic"] = sender;
        await using var creator = CreateCreator((_, _) => { sender.TopicExists = true; return Task.CompletedTask; });
        var dispatcher = CreateDispatcher(creator, sender, client: defaultClient);
        var transportTransaction = new TransportTransaction();
        using var nativeTransaction = new AzureServiceBusTransportTransaction(receiverClient, "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);

        await dispatcher.Dispatch(PublishOperations(), transportTransaction);
        nativeTransaction.Commit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaultClient.Senders["sometopic"].BatchSentMessages, Is.Empty);
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(sender.Transactions, Has.Count.EqualTo(3));
            Assert.That(sender.Transactions.All(id => !string.IsNullOrEmpty(id)), Is.True);
            Assert.That(sender.Transactions.Distinct().Count(), Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_warn_once_per_topic_without_creating_when_disabled(bool sendIndividually)
    {
        var output = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var scope = LogManager.Use<TestingLoggerFactory>().BeginScope(output, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete
        var calls = 0;
        await using var creator = CreateCreator((_, _) => { calls++; return Task.CompletedTask; }, enabled: false);
        var sender = new MissingTopicSender { TryAdd = _ => !sendIndividually };
        var dispatcher = CreateDispatcher(creator, sender);

        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(typeof(SomeOtherEvent)), new TransportTransaction());
        await dispatcher.Dispatch(PublishOperations(typeof(SomeOtherEvent)), new TransportTransaction());

        var entries = DispatcherEntries(output);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.Zero);
            Assert.That(sender.Attempts, Is.EqualTo(4));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic sometopic ")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level == "WARN" && e.Message.Contains("topic othertopic ")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_warn_again_for_same_topic_in_a_new_endpoint_run()
    {
        var output = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var scope = LogManager.Use<TestingLoggerFactory>().BeginScope(output, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete
        await using var creator = CreateCreator(static (_, _) => Task.CompletedTask, enabled: false);
        var first = CreateDispatcher(creator, new MissingTopicSender());
        var second = CreateDispatcher(creator, new MissingTopicSender());

        await first.Dispatch(PublishOperations(), new TransportTransaction());
        await first.Dispatch(PublishOperations(), new TransportTransaction());
        Assert.That(DispatcherEntries(output).Count(e => e.Level == "WARN"), Is.EqualTo(1));
        await second.Dispatch(PublishOperations(), new TransportTransaction());
        await second.Dispatch(PublishOperations(), new TransportTransaction());
        Assert.That(DispatcherEntries(output).Count(e => e.Level == "WARN"), Is.EqualTo(2));
    }

    [Test]
    public async Task Should_warn_and_throw_without_creating_when_throw_mode_is_enabled()
    {
        var output = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var scope = LogManager.Use<TestingLoggerFactory>().BeginScope(output, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete
        var calls = 0;
        await using var creator = CreateCreator((_, _) => { calls++; return Task.CompletedTask; });
        var dispatcher = CreateDispatcher(creator, new MissingTopicSender(), throwOnMissingTopic: true);

        Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(PublishOperations(), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.Zero);
            Assert.That(DispatcherEntries(output).Count(e => e.Level == "WARN"), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Should_not_create_a_topic_for_a_unicast_failure()
    {
        var calls = 0;
        await using var creator = CreateCreator((_, _) => { calls++; return Task.CompletedTask; });
        var dispatcher = CreateDispatcher(creator, new MissingTopicSender());
        var operation = new TransportOperation(new OutgoingMessage("id", [], ReadOnlyMemory<byte>.Empty), new UnicastAddressTag("sometopic"), []);

        Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(new TransportOperations(operation), new TransportTransaction()));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public async Task Should_cancel_pending_creation_and_publish_on_shutdown()
    {
        var entered = Signal();
        var released = Signal();
        var creator = CreateCreator(async (_, token) =>
        {
            entered.TrySetResult();
            await released.Task.WaitAsync(token);
        });
        var dispatcher = CreateDispatcher(creator, new MissingTopicSender());
        var publish = dispatcher.Dispatch(PublishOperations(), new TransportTransaction());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await creator.DisposeAsync();
        Assert.That(async () => await publish, Throws.InstanceOf<OperationCanceledException>());
    }

    static MissingTopicCreator CreateCreator(Func<string, CancellationToken, Task> action, bool enabled = true) =>
        new(action, enabled, TimeSpan.FromSeconds(5), TimeSpan.Zero);

    static MessageDispatcher CreateDispatcher(MissingTopicCreator creator, MissingTopicSender sender,
        bool throwOnMissingTopic = false, FakeServiceBusClient client = null)
    {
        client ??= new FakeServiceBusClient();
        client.Senders["sometopic"] = client.Senders.GetValueOrDefault("sometopic", sender);
        client.Senders["othertopic"] = sender;
        return new MessageDispatcher(client, new MessageSenderRegistry(),
            TopicTopology.FromOptions(new TopologyOptions
            {
                PublishedEventToTopicsMap =
                {
                    { typeof(SomeEvent).FullName, "sometopic" },
                    { typeof(SomeOtherEvent).FullName, "othertopic" }
                }
            }),
            new DestinationManager(HierarchyNamespaceOptions.None), throwOnMissingTopic, creator);
    }

    static TransportOperations PublishOperations(Type eventType = null, int count = 1) =>
        new(Enumerable.Range(0, count).Select(_ => new TransportOperation(
            new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(eventType ?? typeof(SomeEvent)), [], DispatchConsistency.Default)).ToArray());

    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static ServiceBusException MissingTopic() => new("Missing", ServiceBusFailureReason.MessagingEntityNotFound);

    static (string Level, string Message)[] DispatcherEntries(StringWriter output) =>
        [.. LogEntry.Matches(output.ToString())
            .Where(m => m.Groups["logger"].Value == DispatcherLoggerName)
            .Select(m => (m.Groups["level"].Value, m.Groups["message"].Value))];

    const string DispatcherLoggerName = "NServiceBus.Transport.AzureServiceBus.MessageDispatcher";
    static readonly Regex LogEntry = new(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (?<level>[A-Z]+)\s+(?<logger>\S+) (?<message>.*?)(?=\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} |\z)", RegexOptions.Singleline);

    class MissingTopicSender : FakeSender
    {
        public MissingTopicSender()
        {
            SendMessageBatchAction = batch => { AttemptedBatches.Add(batch); AttemptSend(); };
            SendMessageAction = message => { AttemptedMessages.Add(message); AttemptSend(); };
        }

        public volatile bool TopicExists;
        public int FailuresRemaining;
        public int BatchCreationFailures;
        public int BatchCreationAttempts;
        public int Attempts;
        public ServiceBusFailureReason? FailureAfterCreation;
        public readonly List<ServiceBusMessageBatch> AttemptedBatches = [];
        public readonly List<ServiceBusMessage> AttemptedMessages = [];
        public readonly List<string> Transactions = [];

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
        {
            BatchCreationAttempts++;
            if (BatchCreationFailures-- > 0)
            {
                throw MissingTopic();
            }
            return base.CreateMessageBatchAsync(cancellationToken);
        }

        public override Task SendMessagesAsync(ServiceBusMessageBatch batch, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                return base.SendMessagesAsync(batch, cancellationToken);
            }
        }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                return base.SendMessageAsync(message, cancellationToken);
            }
        }

        readonly object sync = new();

        void AttemptSend()
        {
            Attempts++;
            Transactions.Add(Transaction.Current?.TransactionInformation.LocalIdentifier);
            if (FailuresRemaining-- > 0 || !TopicExists)
            {
                throw MissingTopic();
            }
            if (FailureAfterCreation is { } reason)
            {
                throw new ServiceBusException("Different failure", reason);
            }
        }
    }

    class SomeEvent;
    class SomeOtherEvent;
}