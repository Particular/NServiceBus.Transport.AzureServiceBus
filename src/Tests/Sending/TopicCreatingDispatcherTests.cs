namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
public class TopicCreatingDispatcherTests
{
    [Test]
    public async Task Should_dispatch_the_original_operations_once_after_setting_up_every_topic()
    {
        var inner = new ControlledDispatcher();
        var checks = new ConcurrentQueue<string>();
        var dispatcher = CreateControlledDispatcher(inner, (topic, _) =>
        {
            checks.Enqueue(topic);
            return Task.FromResult(true);
        });
        var operations = Publish(typeof(EventA), typeof(EventA), typeof(EventB));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));

        var dispatch = dispatcher.Dispatch(operations, new TransportTransaction());
        await inner.WaitForStarted(1);
        inner.CompleteAll();
        await dispatch;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.Operations.Single(), Is.SameAs(operations));
            Assert.That(checks, Is.EquivalentTo(new[] { "topic-a", "topic-b" }));
        }
    }

    [Test]
    public void Should_not_send_anything_when_setting_up_a_topic_fails()
    {
        var inner = new ControlledDispatcher();
        var failure = new ServiceBusException("Check failed", ServiceBusFailureReason.ServiceBusy);
        var dispatcher = CreateControlledDispatcher(inner, (topic, _) => topic == "topic-b" ? Task.FromException<bool>(failure) : Task.FromResult(true));
        var operations = Publish(typeof(EventA), typeof(EventB));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(operations, new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(inner.Started, Is.Zero, "Queue sends and set-up topics must not go out ahead of a failed setup");
        }
    }

    [Test]
    public async Task Should_share_one_setup_between_concurrent_first_publishes()
    {
        var inner = new ControlledDispatcher();
        var check = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var checks = 0;
        var dispatcher = CreateControlledDispatcher(inner, (_, _) =>
        {
            Interlocked.Increment(ref checks);
            return check.Task;
        });

        var dispatches = Enumerable.Range(0, 3).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction())).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(inner.Started, Is.Zero);
        }

        check.SetResult(true);
        await inner.WaitForStarted(3);
        Assert.That(inner.MaxConcurrent, Is.EqualTo(3), "Publishes waiting for the same setup dispatch together once it completes");

        inner.CompleteAll();
        await Task.WhenAll(dispatches);
    }

    [Test]
    public async Task Should_stop_waiting_for_a_shared_setup_when_cancelled_without_cancelling_it_for_others()
    {
        var inner = new ControlledDispatcher();
        var check = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var checks = 0;
        var dispatcher = CreateControlledDispatcher(inner, (_, _) =>
        {
            Interlocked.Increment(ref checks);
            return check.Task;
        });
        using var cancellation = new CancellationTokenSource();
        var cancelled = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);
        var waiting = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        await cancellation.CancelAsync();
        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());

        check.SetResult(true);
        await inner.WaitForStarted(1);
        inner.CompleteAll();
        await waiting;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(inner.Started, Is.EqualTo(1), "The cancelled dispatch must not reach the inner dispatcher");
        }
    }

    [Test]
    public void Should_let_the_inner_dispatcher_enforce_the_transaction_limit_across_topics()
    {
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = new MissingTopicSender { TopicExists = true };
        client.Senders["topic-b"] = new MissingTopicSender { TopicExists = true };
        var topology = Topology();
        var destinationManager = new DestinationManager(HierarchyNamespaceOptions.None);
        var dispatcher = new TopicCreatingDispatcher(
            new MessageDispatcher(client, new MessageSenderRegistry(), topology, destinationManager, throwOnMissingTopic: true),
            topology, destinationManager, static (_, _) => Task.FromResult(true), static (_, _) => Task.CompletedTask,
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System));
        var transportTransaction = new TransportTransaction();
        using var _ = new AzureServiceBusTransportTransaction(client, "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);
        var operations = Publish([.. Enumerable.Repeat(typeof(EventA), 51).Concat(Enumerable.Repeat(typeof(EventB), 51))]);

        var exception = Assert.ThrowsAsync<Exception>(() => dispatcher.Dispatch(operations, transportTransaction));
        Assert.That(exception.Message, Does.Contain("exceeds the limits"));
    }

    [Test]
    public async Task Should_create_a_missing_topic_once_and_deliver_concurrent_first_publishes()
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender();
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction())));

        var entries = DispatcherEntries(output);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(creation.Calls, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.EqualTo(5), "Provisioning happens before the first send");
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(5));
            Assert.That(entries.Count(e => e.Level == "INFO" && e.Message.Contains("Created topic topic-a")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Should_propagate_post_setup_404_without_retrying_or_logging(bool sendIndividually)
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender { FailuresRemaining = 4, TryAdd = _ => !sendIndividually };
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(creation.Calls, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.EqualTo(1));
            Assert.That(sender.BatchSentMessages, Is.Empty);
            Assert.That(sender.IndividuallySentMessages, Is.Empty);
            Assert.That(DispatcherEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [Test]
    public async Task Should_leave_queued_messages_intact_for_a_caller_retry_after_batch_creation_fails()
    {
        var sender = new MissingTopicSender { BatchCreationFailures = 1 };
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);
        var operations = Publish(typeof(EventA), typeof(EventA), typeof(EventA));

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(operations, new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.Zero);
        }

        await dispatcher.Dispatch(operations, new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(creation.Calls, Is.EqualTo(1));
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(2));
            Assert.That(sender[sender.BatchSentMessages.Single()], Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task Should_propagate_a_creation_failure_and_create_again_on_a_later_publish()
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender();
        var calls = 0;
        var dispatcher = CreateDispatcher(sender, (_, _) =>
        {
            if (++calls == 1)
            {
                return Task.FromException(new UnauthorizedAccessException("No Manage rights"));
            }

            sender.TopicExists = true;
            return Task.CompletedTask;
        });

        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        Assert.That(failure.InnerException, Is.InstanceOf<UnauthorizedAccessException>());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(DispatcherEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [Test]
    public void Should_not_swallow_a_missing_entity_error_from_provisioning()
    {
        var dispatcher = CreateDispatcher(new MissingTopicSender(), static (_, _) => Task.FromException(MissingTopic()));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        Assert.That(exception.InnerException, Is.InstanceOf<ServiceBusException>());
    }

    [Test]
    public async Task Should_deliver_when_another_participant_created_the_topic()
    {
        var sender = new MissingTopicSender();
        var dispatcher = CreateDispatcher(sender, (_, _) =>
        {
            sender.TopicExists = true;
            return Task.FromException(new ServiceBusException("Already created", ServiceBusFailureReason.MessagingEntityAlreadyExists));
        });

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public void Should_not_retry_a_different_delivery_failure()
    {
        var sender = new MissingTopicSender();
        var dispatcher = CreateDispatcher(sender, (_, _) =>
        {
            sender.TopicExists = true;
            sender.FailureAfterCreation = ServiceBusFailureReason.MessageSizeExceeded;
            return Task.CompletedTask;
        });

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessageSizeExceeded));
            Assert.That(sender.Attempts, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_keep_the_single_delivery_attempt_on_the_receivers_client()
    {
        var receiverClient = new FakeServiceBusClient();
        var sender = new MissingTopicSender();
        receiverClient.Senders["topic-a"] = sender;
        var defaultSender = new MissingTopicSender();
        var dispatcher = CreateDispatcher(defaultSender, (_, _) => { sender.TopicExists = true; return Task.CompletedTask; });
        var transportTransaction = new TransportTransaction();
        using var nativeTransaction = new AzureServiceBusTransportTransaction(receiverClient, "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);

        await dispatcher.Dispatch(Publish(typeof(EventA)), transportTransaction);
        nativeTransaction.Commit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaultSender.Attempts, Is.Zero);
            Assert.That(defaultSender.BatchCreationAttempts, Is.Zero);
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(1));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(sender.Transactions.Single(), Is.Not.Null.And.Not.Empty, "The send ran in the receive transaction");
        }
    }

    [Test]
    public async Task Should_not_recreate_a_confirmed_topic_that_was_deleted_during_the_run()
    {
        var sender = new MissingTopicSender();
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        sender.TopicExists = false;
        var first = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        var second = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(second.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(creation.Calls, Is.EqualTo(1));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Should_not_create_a_topic_for_a_failed_send()
    {
        var calls = 0;
        var dispatcher = CreateDispatcher(new MissingTopicSender(), (_, _) => { calls++; return Task.CompletedTask; }, queue: "topic-a");
        var send = new TransportOperation(new OutgoingMessage("id", [], ReadOnlyMemory<byte>.Empty), new UnicastAddressTag("topic-a"), []);

        Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(new TransportOperations(send), new TransportTransaction()));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public async Task Should_stop_waiting_for_a_pending_creation_when_cancelled()
    {
        var entered = Signal();
        var never = Signal();
        var dispatcher = CreateDispatcher(new MissingTopicSender(), async (_, token) =>
        {
            entered.TrySetResult();
            await never.Task.WaitAsync(token);
        });
        using var cancellation = new CancellationTokenSource();
        var publish = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cancellation.CancelAsync();

        Assert.That(async () => await publish, Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    public void Should_create_missing_topics_only_with_installers_and_without_throw_mode(bool setupInfrastructure, bool throwOnMissingTopic, bool creates)
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default) { ThrowOnMissingTopicWhenPublishing = throwOnMissingTopic };
        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, setupInfrastructure);
        var infrastructure = new AzureServiceBusTransportInfrastructure(transport, hostSettings, [], new FakeServiceBusClient(), null,
            new RecordingServiceBusAdministrationClient(), new DestinationManager(HierarchyNamespaceOptions.None));

        Assert.That(infrastructure.Dispatcher, creates ? Is.InstanceOf<TopicCreatingDispatcher>() : Is.InstanceOf<MessageDispatcher>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_create_topic_with_ordering_matching_the_publishers_session_setting(bool enableSessions)
    {
        var recorded = new StringBuilder();
        var client = new FakeServiceBusClient();
        var sender = new MissingTopicSender { TopicExists = true };
        client.Senders["topic-a"] = sender;
        var transport = new AzureServiceBusTransport("connectionString", Topology()) { EnableSessions = enableSessions };
        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, true);
        var infrastructure = new AzureServiceBusTransportInfrastructure(transport, hostSettings, [], client, null,
            new RecordingServiceBusAdministrationClient(recorded), new DestinationManager(HierarchyNamespaceOptions.None));

        await infrastructure.Dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.ToString(), Does.Contain($"\"SupportOrdering\": {(enableSessions ? "true" : "false")}"));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    static TopicCreatingDispatcher CreateControlledDispatcher(IMessageDispatcher inner, Func<string, CancellationToken, Task<bool>> topicExists) =>
        new(inner, Topology(), new DestinationManager(HierarchyNamespaceOptions.None),
            topicExists, static (_, _) => Task.CompletedTask,
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System));

    static TopicCreatingDispatcher CreateDispatcher(MissingTopicSender sender, Func<string, CancellationToken, Task> createTopic, string queue = null)
    {
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = sender;
        if (queue is not null)
        {
            client.Senders[queue] = sender;
        }

        var topology = Topology();
        var destinationManager = new DestinationManager(HierarchyNamespaceOptions.None);
        var inner = new MessageDispatcher(client, new MessageSenderRegistry(), topology, destinationManager, throwOnMissingTopic: true);
        return new TopicCreatingDispatcher(inner, topology, destinationManager,
            (_, _) => Task.FromResult(sender.TopicExists), createTopic,
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System));
    }

    static TopicTopology Topology() => TopicTopology.FromOptions(new TopologyOptions
    {
        PublishedEventToTopicsMap =
        {
            { typeof(EventA).FullName, "topic-a" },
            { typeof(EventB).FullName, "topic-b" }
        }
    });

    static TransportOperations Publish(params Type[] eventTypes) =>
        new([.. eventTypes.Select(eventType => new TransportOperation(
            new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(eventType), [], DispatchConsistency.Default))]);

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

    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    static ServiceBusException MissingTopic() => new("Missing", ServiceBusFailureReason.MessagingEntityNotFound);

    static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Yield();
            timeout.Token.ThrowIfCancellationRequested();
        }
    }

    class TopicCreation(MissingTopicSender sender)
    {
        int calls;

        public int Calls => Volatile.Read(ref calls);

        public Func<string, CancellationToken, Task> Create => (_, _) =>
        {
            Interlocked.Increment(ref calls);
            sender.TopicExists = true;
            return Task.CompletedTask;
        };
    }

    // Each Dispatch call blocks until the test completes or fails it, in the order the calls started
    class ControlledDispatcher : IMessageDispatcher
    {
        public int Started => calls.Count;

        public int MaxConcurrent => Volatile.Read(ref maxConcurrent);

        public IEnumerable<TransportOperations> Operations => operations;

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            var call = Signal();
            var current = Interlocked.Increment(ref concurrent);
            int max;
            while (current > (max = Volatile.Read(ref maxConcurrent)) && Interlocked.CompareExchange(ref maxConcurrent, current, max) != max)
            {
            }

            operations.Enqueue(outgoingMessages);
            calls.Enqueue(call);
            try
            {
                await call.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref concurrent);
            }
        }

        public Task WaitForStarted(int count) => WaitUntil(() => Started >= count);

        public void CompleteAll()
        {
            foreach (var call in calls)
            {
                call.TrySetResult();
            }
        }

        readonly ConcurrentQueue<TaskCompletionSource> calls = new();
        readonly ConcurrentQueue<TransportOperations> operations = new();
        int concurrent;
        int maxConcurrent;
    }

    class MissingTopicSender : FakeSender
    {
        public MissingTopicSender()
        {
            SendMessageBatchAction = _ => AttemptSend();
            SendMessageAction = _ => AttemptSend();
        }

        public volatile bool TopicExists;
        public int FailuresRemaining;
        public int BatchCreationFailures;
        public int BatchCreationAttempts;
        public int Attempts;
        public ServiceBusFailureReason? FailureAfterCreation;
        public readonly List<string> Transactions = [];

        public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                BatchCreationAttempts++;
                if (BatchCreationFailures-- > 0)
                {
                    throw MissingTopic();
                }
            }

            return base.CreateMessageBatchAsync(cancellationToken);
        }

        public override Task SendMessagesAsync(ServiceBusMessageBatch messageBatch, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                return base.SendMessagesAsync(messageBatch, cancellationToken);
            }
        }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                return base.SendMessageAsync(message, cancellationToken);
            }
        }

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

        readonly object sync = new();
    }

    class EventA;

    class EventB;
}