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
    public async Task Should_let_one_dispatch_at_a_time_publish_to_an_unconfirmed_topic()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);

        var dispatches = Enumerable.Range(0, 3).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction())).ToArray();
        await inner.WaitForStarted(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.Started, Is.EqualTo(1));
            Assert.That(dispatches.Count(d => d.IsCompleted), Is.Zero);
        }

        inner.Complete(0);
        await inner.WaitForStarted(3);
        Assert.That(inner.MaxConcurrent, Is.EqualTo(2), "Dispatches queued before confirmation run concurrently once the topic is confirmed");

        inner.CompleteAll();
        await Task.WhenAll(dispatches);
    }

    [Test]
    public async Task Should_keep_serializing_after_a_failed_dispatch()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);

        var first = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        var second = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        var third = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await inner.WaitForStarted(1);

        inner.Fail(0);
        Assert.That(async () => await first, Throws.InstanceOf<InvalidOperationException>());
        await inner.WaitForStarted(2);
        Assert.That(inner.Started, Is.EqualTo(2), "A failure must not confirm the topic");

        inner.Complete(1);
        await inner.WaitForStarted(3);
        inner.CompleteAll();
        await Task.WhenAll(second, third);
        Assert.That(inner.MaxConcurrent, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_not_gate_or_split_dispatches_once_topics_are_confirmed()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);
        var first = dispatcher.Dispatch(Publish(typeof(EventA), typeof(EventB)), new TransportTransaction());
        await inner.WaitForStarted(2);
        inner.CompleteAll();
        await first;

        var dispatches = Enumerable.Range(0, 3).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA), typeof(EventB)), new TransportTransaction())).ToArray();
        await inner.WaitForStarted(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.MaxConcurrent, Is.EqualTo(3));
            Assert.That(inner.Operations.Skip(2).All(o => o.MulticastTransportOperations.Count == 2), Is.True, "Confirmed topics go out in one dispatch");
        }
        inner.CompleteAll();
        await Task.WhenAll(dispatches);
    }

    [Test]
    public async Task Should_dispatch_each_unconfirmed_topic_separately_from_the_rest()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);
        var operations = Publish(typeof(EventA), typeof(EventA), typeof(EventB));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));

        var dispatch = dispatcher.Dispatch(operations, new TransportTransaction());
        await inner.WaitForStarted(3);
        inner.CompleteAll();
        await dispatch;

        var dispatched = inner.Operations.ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dispatched, Has.Length.EqualTo(3));
            Assert.That(dispatched.Count(o => o.UnicastTransportOperations.Count == 1 && o.MulticastTransportOperations.Count == 0), Is.EqualTo(1));
            Assert.That(dispatched.Count(o => o.MulticastTransportOperations.Count == 2 && o.MulticastTransportOperations.All(m => m.MessageType == typeof(EventA))), Is.EqualTo(1));
            Assert.That(dispatched.Count(o => o.MulticastTransportOperations.Count == 1 && o.MulticastTransportOperations[0].MessageType == typeof(EventB)), Is.EqualTo(1));
            Assert.That(dispatched.SelectMany(o => o.MulticastTransportOperations), Is.SubsetOf(operations.MulticastTransportOperations), "The original operations are dispatched, not copies");
        }
    }

    [Test]
    public async Task Should_not_hold_back_other_operations_while_a_topic_is_gated()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);
        var holdsTopic = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await inner.WaitForStarted(1);

        var operations = Publish(typeof(EventA));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));
        var mixed = dispatcher.Dispatch(operations, new TransportTransaction());
        await inner.WaitForStarted(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.Operations.ElementAt(1).UnicastTransportOperations, Has.Count.EqualTo(1));
            Assert.That(inner.Operations.ElementAt(1).MulticastTransportOperations, Is.Empty);
            Assert.That(mixed.IsCompleted, Is.False);
        }

        inner.CompleteAll();
        await inner.WaitForStarted(3);
        inner.CompleteAll();
        await Task.WhenAll(holdsTopic, mixed);
    }

    [Test]
    public async Task Should_not_split_a_transactional_dispatch_over_the_transaction_limit()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);
        var transportTransaction = new TransportTransaction();
        using var _ = new AzureServiceBusTransportTransaction(new FakeServiceBusClient(), "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);

        // Split per topic, each part would stay under the limit and the inner dispatcher wouldn't report it
        var half = (MessageDispatcher.MaxMessageThresholdForTransaction / 2) + 1;
        var operations = Publish([.. Enumerable.Repeat(typeof(EventA), half).Concat(Enumerable.Repeat(typeof(EventB), half))]);

        var dispatch = dispatcher.Dispatch(operations, transportTransaction);
        await inner.WaitForStarted(1);
        inner.CompleteAll();
        await dispatch;

        Assert.That(inner.Operations.Single().MulticastTransportOperations, Has.Count.EqualTo(half * 2));
    }

    [Test]
    public async Task Should_stop_waiting_when_cancelled_and_let_others_proceed()
    {
        var inner = new ControlledDispatcher();
        var dispatcher = CreateGatedDispatcher(inner);
        var first = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await inner.WaitForStarted(1);

        using var cancellation = new CancellationTokenSource();
        var cancelled = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);
        var waiting = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await cancellation.CancelAsync();
        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());

        inner.Complete(0);
        await inner.WaitForStarted(2);
        inner.CompleteAll();
        await Task.WhenAll(first, waiting);
        Assert.That(inner.Started, Is.EqualTo(2), "The cancelled dispatch must not reach the inner dispatcher");
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
    public async Task Should_retry_stale_404s_after_creation(bool sendIndividually)
    {
        var sender = new MissingTopicSender { FailuresRemaining = 4, TryAdd = _ => !sendIndividually };
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(creation.Calls, Is.EqualTo(1), "Post-creation 404s must not create the topic again");
            Assert.That(sender.Attempts, Is.EqualTo(5));
            Assert.That(sendIndividually ? sender.IndividuallySentMessages.Count : sender.BatchSentMessages.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_recover_a_failed_batch_creation_without_losing_messages()
    {
        var sender = new MissingTopicSender { BatchCreationFailures = 3 };
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);

        await dispatcher.Dispatch(Publish(typeof(EventA), typeof(EventA), typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(creation.Calls, Is.EqualTo(1));
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(4));
            Assert.That(sender[sender.BatchSentMessages.Single()], Has.Count.EqualTo(3));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Should_throw_without_logging_when_delivery_attempts_are_exhausted(bool sendIndividually)
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var sender = new MissingTopicSender { TryAdd = _ => !sendIndividually };
        var calls = 0;
        var dispatcher = CreateDispatcher(sender, (_, _) => { calls++; return Task.CompletedTask; });

        var exception = Assert.ThrowsAsync<TimeoutException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.InnerException, Is.InstanceOf<ServiceBusException>());
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(sender.Attempts, Is.EqualTo(TopicCreatingDispatcher.MaxDeliveryAttempts));
            Assert.That(sender.BatchSentMessages, Is.Empty);
            Assert.That(sender.IndividuallySentMessages, Is.Empty);
            Assert.That(DispatcherEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [Test]
    public async Task Should_bound_the_wait_for_creation()
    {
        var entered = Signal();
        var release = Signal();
        var dispatcher = CreateDispatcher(new MissingTopicSender(), async (_, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        }, TimeSpan.FromMilliseconds(100));

        var publish = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = Assert.ThrowsAsync<TimeoutException>(async () => await publish.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(exception.Message, Does.Contain($"did not recover within {0.1:0.###} seconds"));
        release.TrySetResult();
    }

    [Test]
    public async Task Should_bound_the_wait_for_an_unresponsive_delivery()
    {
        var blocked = Signal();
        var inner = new DelegateDispatcher(attempt => attempt == 1 ? Task.FromException(MissingTopic()) : blocked.Task);
        var dispatcher = new TopicCreatingDispatcher(inner, Topology(), new DestinationManager(HierarchyNamespaceOptions.None),
            static (_, _) => Task.FromResult(true), static (_, _) => Task.CompletedTask,
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System),
            TimeSpan.FromMilliseconds(100), TimeSpan.Zero, TimeProvider.System);

        try
        {
            var exception = Assert.ThrowsAsync<TimeoutException>(async () => await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()).WaitAsync(TimeSpan.FromSeconds(5)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception.Message, Does.Contain($"did not recover within {0.1:0.###} seconds"));
                Assert.That(inner.Attempts, Is.EqualTo(2), "A timed-out dispatch must not start another retry");
            }
        }
        finally
        {
            blocked.TrySetResult();
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

    // The SDK opens the link in CreateMessageBatchAsync, before the dispatcher enters the transaction scope, so a missing
    // topic fails there. A failure inside the scope would abort the transaction and is not retried.
    [Test]
    public async Task Should_keep_transactional_retries_on_the_receivers_client()
    {
        var receiverClient = new FakeServiceBusClient();
        var sender = new MissingTopicSender { BatchCreationFailures = 2 };
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
            Assert.That(sender.BatchCreationAttempts, Is.EqualTo(3));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(sender.Transactions.Single(), Is.Not.Null.And.Not.Empty, "The send ran in the receive transaction");
        }
    }

    [Test]
    public async Task Should_throw_when_a_confirmed_topic_goes_missing_and_create_it_on_the_next_publish()
    {
        var sender = new MissingTopicSender();
        var creation = new TopicCreation(sender);
        var dispatcher = CreateDispatcher(sender, creation.Create);
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        sender.TopicExists = false;
        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(creation.Calls, Is.EqualTo(2));
            Assert.That(sender.BatchSentMessages, Has.Count.EqualTo(2));
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
    public async Task Should_cancel_a_pending_creation_with_the_caller()
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
        var sender = new MissingTopicSender { TopicExists = true, FailuresRemaining = 1 };
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

    static TopicCreatingDispatcher CreateGatedDispatcher(IMessageDispatcher inner) =>
        new(inner, Topology(), new DestinationManager(HierarchyNamespaceOptions.None),
            static (_, _) => Task.FromResult(true), static (_, _) => Task.CompletedTask,
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System),
            TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeProvider.System);

    static TopicCreatingDispatcher CreateDispatcher(MissingTopicSender sender, Func<string, CancellationToken, Task> createTopic,
        TimeSpan? recoveryTimeout = null, string queue = null)
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
            new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, TimeProvider.System),
            recoveryTimeout ?? TimeSpan.FromSeconds(5), TimeSpan.Zero, TimeProvider.System);
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

    class DelegateDispatcher(Func<int, Task> onAttempt) : IMessageDispatcher
    {
        int attempts;

        public int Attempts => Volatile.Read(ref attempts);

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default) =>
            onAttempt(Interlocked.Increment(ref attempts));
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

        public void Complete(int index) => calls.ElementAt(index).TrySetResult();

        public void Fail(int index) => calls.ElementAt(index).TrySetException(new InvalidOperationException("Dispatch failed"));

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