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
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using NServiceBus.Logging;
using NServiceBus.Testing;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Routing;

[TestFixture]
public class TopicCreationTests
{
    [Test]
    public async Task Should_check_every_published_topic_once_before_sending_anything()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var topicB = new MissingTopicSender { TopicExists = true };
        var queue = new FakeSender();
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = topicA;
        client.Senders["topic-b"] = topicB;
        client.Senders["somequeue"] = queue;
        var checks = new ConcurrentQueue<(string Topic, int SentSoFar)>();
        var administrationClient = new FakeAdministrationClient
        {
            TopicExists = (topic, _) =>
            {
                checks.Enqueue((topic, topicA.Attempts + topicB.Attempts + queue.BatchSentMessages.Count));
                return Task.FromResult(true);
            }
        };
        var operations = Publish(typeof(EventA), typeof(EventA), typeof(EventB));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));

        await CreateDispatcher(administrationClient, client).Dispatch(operations, new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checks.Select(c => c.Topic), Is.EquivalentTo(new[] { "topic-a", "topic-b" }));
            Assert.That(checks.Select(c => c.SentSoFar), Is.All.Zero);
            Assert.That(topicA[topicA.BatchSentMessages.Single()], Has.Count.EqualTo(2));
            Assert.That(topicB.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(queue.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Should_not_send_anything_when_setting_up_a_topic_fails()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var queue = new FakeSender();
        var client = ClientWith(topicA);
        client.Senders["somequeue"] = queue;
        var failure = new ServiceBusException("Check failed", ServiceBusFailureReason.ServiceBusy);
        var administrationClient = new FakeAdministrationClient
        {
            TopicExists = (topic, _) => topic == "topic-b" ? Task.FromException<bool>(failure) : Task.FromResult(true)
        };
        var operations = Publish(typeof(EventA), typeof(EventB));
        operations.UnicastTransportOperations.Add(new UnicastTransportOperation(new OutgoingMessage("send", [], ReadOnlyMemory<byte>.Empty), "somequeue", []));

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => CreateDispatcher(administrationClient, client).Dispatch(operations, new TransportTransaction()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(topicA.Attempts, Is.Zero, "Set-up topics must not go out ahead of a failed setup");
            Assert.That(queue.BatchSentMessages, Is.Empty, "Queue sends must not go out ahead of a failed setup");
        }
    }

    [Test]
    public async Task Should_check_a_topic_once_and_not_again_after_it_exists()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var administrationClient = Namespace(topicA);
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(administrationClient.Creates, Is.Zero);
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(3));
        }

        await dispatcher.Dispatch(Publish(typeof(EventB)), new TransportTransaction());
        Assert.That(administrationClient.Checks, Is.EqualTo(2));
    }

    [TestCase(ServiceBusFailureReason.ServiceBusy)]
    [TestCase(ServiceBusFailureReason.ServiceCommunicationProblem)]
    public async Task Should_propagate_a_failed_check_without_creating_and_check_again_next_time(ServiceBusFailureReason reason)
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var failure = new ServiceBusException("Check failed", reason);
        var reads = 0;
        var administrationClient = new FakeAdministrationClient
        {
            TopicExists = (_, _) => ++reads == 1 ? Task.FromException<bool>(failure) : Task.FromResult(true)
        };
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(administrationClient.Creates, Is.Zero);
            Assert.That(topicA.Attempts, Is.Zero);
        }

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(2));
            Assert.That(administrationClient.Creates, Is.Zero);
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_share_one_check_between_concurrent_first_publishes()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var check = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var administrationClient = new FakeAdministrationClient { TopicExists = (_, _) => check.Task };
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        var dispatches = Enumerable.Range(0, 3).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction())).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(topicA.Attempts, Is.Zero);
        }

        check.SetResult(true);
        await Task.WhenAll(dispatches);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task Should_let_a_caller_stop_waiting_for_another_callers_setup()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var check = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var administrationClient = new FakeAdministrationClient { TopicExists = (_, _) => check.Task };
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));
        using var cancellation = new CancellationTokenSource();
        var settingUp = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        var waiting = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);

        await cancellation.CancelAsync();
        Assert.That(async () => await waiting, Throws.InstanceOf<OperationCanceledException>());

        check.SetResult(true);
        await settingUp;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1), "The cancelled dispatch must not send");
        }
    }

    [Test]
    public async Task Should_check_again_after_the_caller_doing_the_setup_is_cancelled()
    {
        var topicA = new MissingTopicSender { TopicExists = true };
        var administrationClient = new FakeAdministrationClient
        {
            TopicExists = async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return true;
            }
        };
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));
        using var cancellation = new CancellationTokenSource();
        var cancelled = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);

        await cancellation.CancelAsync();
        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());

        administrationClient.TopicExists = static (_, _) => Task.FromResult(true);
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(2), "A cancelled setup must not count as done");
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Should_enforce_the_transaction_limit_before_checking_topics()
    {
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = new MissingTopicSender { TopicExists = true };
        client.Senders["topic-b"] = new MissingTopicSender { TopicExists = true };
        var administrationClient = new FakeAdministrationClient();
        var transportTransaction = new TransportTransaction();
        using var _ = new AzureServiceBusTransportTransaction(client, "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);
        var operations = Publish([.. Enumerable.Repeat(typeof(EventA), 51).Concat(Enumerable.Repeat(typeof(EventB), 51))]);

        var exception = Assert.ThrowsAsync<Exception>(() => CreateDispatcher(administrationClient, client).Dispatch(operations, transportTransaction));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("exceeds the limits"));
            Assert.That(administrationClient.Checks, Is.Zero);
        }
    }

    [Test]
    public async Task Should_create_a_missing_topic_once_before_sending_and_deliver_concurrent_first_publishes()
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var topicA = new MissingTopicSender();
        var sentBeforeCreation = -1;
        var administrationClient = Namespace(topicA, (_, _) =>
        {
            sentBeforeCreation = topicA.Attempts;
            topicA.TopicExists = true;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction())));

        var entries = LogEntries(output);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Creates, Is.EqualTo(1));
            Assert.That(sentBeforeCreation, Is.Zero);
            Assert.That(topicA.Attempts, Is.EqualTo(5));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(5));
            Assert.That(entries.Count(e => e.Level == "INFO" && e.Message.Contains("Created topic topic-a")), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Should_propagate_a_404_after_setup_without_retrying_or_logging(bool sendIndividually)
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var topicA = new MissingTopicSender { FailuresRemaining = 4, TryAdd = _ => !sendIndividually };
        var administrationClient = Namespace(topicA);

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => CreateDispatcher(administrationClient, ClientWith(topicA)).Dispatch(Publish(typeof(EventA)), new TransportTransaction()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(administrationClient.Creates, Is.EqualTo(1));
            Assert.That(topicA.Attempts, Is.EqualTo(1));
            Assert.That(topicA.BatchSentMessages, Is.Empty);
            Assert.That(topicA.IndividuallySentMessages, Is.Empty);
            Assert.That(LogEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [Test]
    public async Task Should_send_again_without_checking_after_a_stale_404_following_setup()
    {
        var topicA = new MissingTopicSender { TopicExists = true, FailuresRemaining = 2 };
        var administrationClient = Namespace(topicA);
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
        }

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(administrationClient.Creates, Is.Zero);
            Assert.That(topicA.Attempts, Is.EqualTo(3));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_leave_queued_messages_intact_for_a_caller_retry_after_batch_creation_fails()
    {
        var topicA = new MissingTopicSender { BatchCreationFailures = 1 };
        var administrationClient = Namespace(topicA);
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));
        var operations = Publish(typeof(EventA), typeof(EventA), typeof(EventA));

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(operations, new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(topicA.BatchCreationAttempts, Is.EqualTo(1));
            Assert.That(topicA.Attempts, Is.Zero);
        }

        await dispatcher.Dispatch(operations, new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Creates, Is.EqualTo(1));
            Assert.That(topicA.BatchCreationAttempts, Is.EqualTo(2));
            Assert.That(topicA[topicA.BatchSentMessages.Single()], Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task Should_propagate_a_creation_failure_and_create_again_on_a_later_publish()
    {
        var output = CaptureLogs(out var scope);
        using var _ = scope;
        var topicA = new MissingTopicSender();
        var calls = 0;
        var administrationClient = Namespace(topicA, (_, _) =>
        {
            if (++calls == 1)
            {
                return Task.FromException(new UnauthorizedAccessException("No Manage rights"));
            }

            topicA.TopicExists = true;
            return Task.CompletedTask;
        });
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));

        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        Assert.That(failure.InnerException, Is.InstanceOf<UnauthorizedAccessException>());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Creates, Is.EqualTo(2));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(LogEntries(output).Count(e => e.Level is "WARN" or "ERROR"), Is.Zero);
        }
    }

    [Test]
    public void Should_not_swallow_a_missing_entity_error_from_creation()
    {
        var topicA = new MissingTopicSender();
        var administrationClient = Namespace(topicA, static (_, _) => Task.FromException(MissingTopic()));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => CreateDispatcher(administrationClient, ClientWith(topicA)).Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        Assert.That(exception.InnerException, Is.InstanceOf<ServiceBusException>());
    }

    [Test]
    public async Task Should_deliver_when_another_participant_created_the_topic()
    {
        var topicA = new MissingTopicSender();
        var administrationClient = Namespace(topicA, (_, _) =>
        {
            topicA.TopicExists = true;
            return Task.FromException(new ServiceBusException("Already created", ServiceBusFailureReason.MessagingEntityAlreadyExists));
        });

        await CreateDispatcher(administrationClient, ClientWith(topicA)).Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public void Should_not_retry_a_different_delivery_failure()
    {
        var topicA = new MissingTopicSender();
        var administrationClient = Namespace(topicA, (_, _) =>
        {
            topicA.TopicExists = true;
            topicA.FailureAfterCreation = ServiceBusFailureReason.MessageSizeExceeded;
            return Task.CompletedTask;
        });

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => CreateDispatcher(administrationClient, ClientWith(topicA)).Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessageSizeExceeded));
            Assert.That(topicA.Attempts, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_set_up_and_send_on_the_receive_transactions_client()
    {
        var receiverClient = new FakeServiceBusClient();
        var receiverSender = new MissingTopicSender();
        receiverClient.Senders["topic-a"] = receiverSender;
        var defaultSender = new MissingTopicSender();
        var administrationClient = Namespace(receiverSender);
        var transportTransaction = new TransportTransaction();
        using var nativeTransaction = new AzureServiceBusTransportTransaction(receiverClient, "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transportTransaction);

        await CreateDispatcher(administrationClient, ClientWith(defaultSender)).Dispatch(Publish(typeof(EventA)), transportTransaction);
        nativeTransaction.Commit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Creates, Is.EqualTo(1));
            Assert.That(defaultSender.Attempts, Is.Zero);
            Assert.That(defaultSender.BatchCreationAttempts, Is.Zero);
            Assert.That(receiverSender.BatchCreationAttempts, Is.EqualTo(1));
            Assert.That(receiverSender.BatchSentMessages, Has.Count.EqualTo(1));
            Assert.That(receiverSender.Transactions.Single(), Is.Not.Null.And.Not.Empty, "The send ran in the receive transaction");
        }
    }

    [Test]
    public async Task Should_not_recreate_a_topic_that_was_deleted_during_the_run()
    {
        var topicA = new MissingTopicSender();
        var administrationClient = Namespace(topicA);
        var dispatcher = CreateDispatcher(administrationClient, ClientWith(topicA));
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        topicA.TopicExists = false;
        var first = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        var second = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(second.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(administrationClient.Checks, Is.EqualTo(1));
            Assert.That(administrationClient.Creates, Is.EqualTo(1));
            Assert.That(topicA.BatchSentMessages, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Should_not_set_up_a_topic_for_a_send()
    {
        var sender = new MissingTopicSender();
        var client = ClientWith(sender);
        var administrationClient = Namespace(sender);
        var send = new TransportOperation(new OutgoingMessage("id", [], ReadOnlyMemory<byte>.Empty), new UnicastAddressTag("topic-a"), []);

        Assert.ThrowsAsync<ServiceBusException>(() => CreateDispatcher(administrationClient, client).Dispatch(new TransportOperations(send), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(administrationClient.Checks, Is.Zero);
            Assert.That(administrationClient.Creates, Is.Zero);
        }
    }

    [Test]
    public async Task Should_stop_waiting_for_a_pending_creation_when_cancelled()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var topicA = new MissingTopicSender();
        var administrationClient = Namespace(topicA, async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        using var cancellation = new CancellationTokenSource();
        var publish = CreateDispatcher(administrationClient, ClientWith(topicA)).Dispatch(Publish(typeof(EventA)), new TransportTransaction(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cancellation.CancelAsync();

        Assert.That(async () => await publish, Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    public async Task Should_set_up_topics_only_with_installers_and_without_throw_mode(bool setupInfrastructure, bool throwOnMissingTopic, bool setsUp)
    {
        var recorded = new StringBuilder();
        var transport = new AzureServiceBusTransport("connectionString", Topology()) { ThrowOnMissingTopicWhenPublishing = throwOnMissingTopic };
        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, setupInfrastructure);
        var infrastructure = new AzureServiceBusTransportInfrastructure(transport, hostSettings, [], new FakeServiceBusClient(),
            new RecordingServiceBusAdministrationClient(recorded), new DestinationManager(HierarchyNamespaceOptions.None));

        await infrastructure.Dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        Assert.That(recorded.ToString(), setsUp ? Does.Contain("TopicExists('topic-a')") : Is.Empty);
    }

    static MessageDispatcher CreateDispatcher(ServiceBusAdministrationClient administrationClient, FakeServiceBusClient client) =>
        new(client, new MessageSenderRegistry(new TopicCreator(administrationClient, false, 1024)), Topology(),
            new DestinationManager(HierarchyNamespaceOptions.None), throwOnMissingTopic: true);

    static FakeServiceBusClient ClientWith(MissingTopicSender topicA)
    {
        var client = new FakeServiceBusClient();
        client.Senders["topic-a"] = topicA;
        return client;
    }

    // Reports whether the sender's topic exists, and creates it unless told otherwise
    static FakeAdministrationClient Namespace(MissingTopicSender sender, Func<CreateTopicOptions, CancellationToken, Task> createTopic = null) => new()
    {
        TopicExists = (_, _) => Task.FromResult(sender.TopicExists),
        CreateTopic = createTopic ?? ((_, _) =>
        {
            sender.TopicExists = true;
            return Task.CompletedTask;
        })
    };

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

    // Entries are "<timestamp> <LEVEL> <logger> <message>" with no separator
    static (string Level, string Message)[] LogEntries(StringWriter output) =>
        [.. LogEntry.Matches(output.ToString())
            .Where(m => m.Groups["logger"].Value.StartsWith("NServiceBus.Transport.AzureServiceBus.", StringComparison.Ordinal))
            .Select(m => (m.Groups["level"].Value, m.Groups["message"].Value))];

    static readonly Regex LogEntry = new(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (?<level>[A-Z]+)\s+(?<logger>\S+) (?<message>.*?)(?=\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} |\z)", RegexOptions.Singleline);

    static ServiceBusException MissingTopic() => new("Missing", ServiceBusFailureReason.MessagingEntityNotFound);

    class FakeAdministrationClient : ServiceBusAdministrationClient
    {
        public Func<string, CancellationToken, Task<bool>> TopicExists { get; set; } = static (_, _) => Task.FromResult(true);

        public Func<CreateTopicOptions, CancellationToken, Task> CreateTopic { get; init; } = static (_, _) => Task.CompletedTask;

        public int Checks => Volatile.Read(ref checks);

        public int Creates => Volatile.Read(ref creates);

        public override async Task<Response<bool>> TopicExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref checks);
            return Response.FromValue(await TopicExists(name, cancellationToken), null);
        }

        public override async Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref creates);
            await CreateTopic(options, cancellationToken);
            return null;
        }

        int checks;
        int creates;
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