namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Routing;

[TestFixture]
public class TopicCreatingDispatcherPreflightTests
{
    [Test]
    public async Task Should_check_existing_topics_once_and_bypass_administration_after_success()
    {
        var reads = 0;
        var creates = 0;
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner, (_, _) => { reads++; return Task.FromResult(true); }, (_, _) => { creates++; return Task.CompletedTask; });

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(creates, Is.Zero);
            Assert.That(inner.Calls, Is.EqualTo(3));
        }

        await dispatcher.Dispatch(Publish(typeof(EventB)), new TransportTransaction());
        Assert.That(reads, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_create_a_missing_topic_before_the_first_dispatch()
    {
        var calls = new List<string>();
        var inner = new RecordingDispatcher(_ => { calls.Add("dispatch"); return Task.CompletedTask; });
        var dispatcher = Create(inner,
            (_, _) => { calls.Add("check"); return Task.FromResult(false); },
            (_, _) => { calls.Add("create"); return Task.CompletedTask; });

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        Assert.That(calls, Is.EqualTo(new[] { "check", "create", "dispatch" }));
    }

    [TestCase(ServiceBusFailureReason.ServiceBusy)]
    [TestCase(ServiceBusFailureReason.ServiceCommunicationProblem)]
    public async Task Should_propagate_a_failed_check_without_creating_and_check_again_next_time(ServiceBusFailureReason reason)
    {
        var reads = 0;
        var creates = 0;
        var failure = new ServiceBusException("Check failed", reason);
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner,
            (_, _) => ++reads == 1 ? Task.FromException<bool>(failure) : Task.FromResult(true),
            (_, _) => { creates++; return Task.CompletedTask; });

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.SameAs(failure));
            Assert.That(creates, Is.Zero);
            Assert.That(inner.Calls, Is.Zero);
        }
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EqualTo(2));
            Assert.That(creates, Is.Zero);
            Assert.That(inner.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_leave_deletion_race_recovery_to_the_next_caller_attempt()
    {
        var calls = new List<string>();
        var reads = 0;
        var inner = new RecordingDispatcher(attempt =>
        {
            calls.Add("dispatch");
            return attempt == 1 ? Task.FromException(Missing()) : Task.CompletedTask;
        });
        var dispatcher = Create(inner,
            (_, _) => { calls.Add("check"); return Task.FromResult(++reads == 1); },
            (_, _) => { calls.Add("create"); return Task.CompletedTask; });

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(calls, Is.EqualTo(new[] { "check", "dispatch" }));
        }

        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        Assert.That(calls, Is.EqualTo(new[] { "check", "dispatch", "check", "create", "dispatch" }));
    }

    [Test]
    public async Task Should_attempt_a_stale_404_once_per_caller_attempt_and_confirm_on_success()
    {
        var reads = 0;
        var creates = 0;
        var inner = new RecordingDispatcher(attempt => attempt <= 3 ? Task.FromException(Missing()) : Task.CompletedTask);
        var dispatcher = Create(inner,
            (_, _) => { reads++; return Task.FromResult(true); },
            (_, _) => { creates++; return Task.CompletedTask; });

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction()));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
                Assert.That(reads, Is.EqualTo(attempt));
                Assert.That(inner.Calls, Is.EqualTo(attempt));
            }
        }
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        await dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EqualTo(4), "Confirmation avoids another check after successful delivery");
            Assert.That(creates, Is.Zero);
            Assert.That(inner.Calls, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Should_share_admin_pacing_across_different_topics()
    {
        var time = new FakeTimeProvider();
        var limiter = new TopicAdministrationLimiter(TimeSpan.FromMilliseconds(200), TimeSpan.Zero, time);
        var reads = 0;
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner,
            (_, _) => { reads++; return Task.FromResult(true); }, static (_, _) => Task.CompletedTask, time, limiter);

        var dispatch = dispatcher.Dispatch(Publish(typeof(EventA), typeof(EventB)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(inner.Calls, Is.EqualTo(1));
            Assert.That(dispatch.IsCompleted, Is.False);
        }
        time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.That(reads, Is.EqualTo(1));
        time.Advance(TimeSpan.FromMilliseconds(1));
        await dispatch;
        Assert.That(reads, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_use_the_same_admission_limit_for_checks_and_creates()
    {
        var time = new FakeTimeProvider();
        var limiter = new TopicAdministrationLimiter(TimeSpan.FromMilliseconds(200), TimeSpan.Zero, time);
        var reads = 0;
        var creates = 0;
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner,
            (_, _) => { reads++; return Task.FromResult(false); },
            (_, _) => { creates++; return Task.CompletedTask; }, time, limiter);

        var dispatch = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(creates, Is.Zero);
            Assert.That(inner.Calls, Is.Zero);
        }
        time.Advance(TimeSpan.FromMilliseconds(200));
        await dispatch;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(creates, Is.EqualTo(1));
            Assert.That(inner.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Should_include_topic_gate_waiting_in_the_deadline()
    {
        var time = new FakeTimeProvider();
        var release = Signal();
        var inner = new RecordingDispatcher(_ => release.Task);
        var dispatcher = Create(inner, static (_, _) => Task.FromResult(true), static (_, _) => Task.CompletedTask, time);
        var first = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        var second = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        Assert.That(inner.Calls, Is.EqualTo(1));

        time.Advance(TimeSpan.FromSeconds(30));
        try
        {
            var firstError = Assert.ThrowsAsync<TimeoutException>(async () => await first.WaitAsync(TimeSpan.FromSeconds(5)));
            var secondError = Assert.ThrowsAsync<TimeoutException>(async () => await second.WaitAsync(TimeSpan.FromSeconds(5)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstError.Message, Does.Contain("did not recover within 30 seconds"));
                Assert.That(secondError.Message, Does.Contain("did not recover within 30 seconds"));
                Assert.That(inner.Calls, Is.EqualTo(1));
            }
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Test]
    public async Task Should_bound_admin_queue_waiting_without_starting_another_sdk_call()
    {
        var time = new FakeTimeProvider();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var creates = 0;
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner,
            (_, _) => { reads++; return release.Task; },
            (_, _) => { creates++; return Task.CompletedTask; }, time);
        var first = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        var second = dispatcher.Dispatch(Publish(typeof(EventB)), new TransportTransaction());
        Assert.That(reads, Is.EqualTo(1));

        time.Advance(TimeSpan.FromSeconds(30));
        try
        {
            Assert.ThrowsAsync<TimeoutException>(async () => await first.WaitAsync(TimeSpan.FromSeconds(5)));
            var exception = Assert.ThrowsAsync<TimeoutException>(async () => await second.WaitAsync(TimeSpan.FromSeconds(5)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception.Message, Does.Contain("did not recover within 30 seconds"));
                Assert.That(reads, Is.EqualTo(1));
                Assert.That(creates, Is.Zero);
                Assert.That(inner.Calls, Is.Zero);
            }
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [Test]
    public async Task Should_count_startup_jitter_against_the_deadline()
    {
        var time = new FakeTimeProvider();
        var limiter = new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.FromSeconds(40), time);
        var reads = 0;
        var inner = new RecordingDispatcher();
        var dispatcher = Create(inner,
            (_, _) => { reads++; return Task.FromResult(true); }, static (_, _) => Task.CompletedTask, time, limiter);
        var dispatch = dispatcher.Dispatch(Publish(typeof(EventA)), new TransportTransaction());
        time.Advance(TimeSpan.FromSeconds(30));
        var exception = Assert.ThrowsAsync<TimeoutException>(async () => await dispatch.WaitAsync(TimeSpan.FromSeconds(5)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("did not recover within 30 seconds"));
            Assert.That(reads, Is.Zero);
            Assert.That(inner.Calls, Is.Zero);
        }
    }

    [Test]
    public void Should_not_retry_when_the_receive_transaction_has_aborted()
    {
        var transaction = new TransportTransaction();
        using var nativeTransaction = new AzureServiceBusTransportTransaction(new FakeServiceBusClient(), "partition",
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) }, transaction);
        var inner = new RecordingDispatcher(_ =>
        {
            nativeTransaction.Transaction.Rollback();
            return Task.FromException(Missing());
        });
        var reads = 0;
        var creates = 0;
        var dispatcher = Create(inner,
            (_, _) => { reads++; return Task.FromResult(true); },
            (_, _) => { creates++; return Task.CompletedTask; });

        var exception = Assert.ThrowsAsync<ServiceBusException>(() => dispatcher.Dispatch(Publish(typeof(EventA)), transaction));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Reason, Is.EqualTo(ServiceBusFailureReason.MessagingEntityNotFound));
            Assert.That(inner.Calls, Is.EqualTo(1));
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(creates, Is.Zero);
        }
    }

    static TopicCreatingDispatcher Create(RecordingDispatcher inner,
        Func<string, CancellationToken, Task<bool>> check, Func<string, CancellationToken, Task> create,
        TimeProvider time = null, TopicAdministrationLimiter limiter = null)
    {
        time ??= TimeProvider.System;
        var topology = TopicTopology.FromOptions(new TopologyOptions
        {
            PublishedEventToTopicsMap =
            {
                { typeof(EventA).FullName, "topic-a" },
                { typeof(EventB).FullName, "topic-b" }
            }
        });
        return new TopicCreatingDispatcher(inner, topology, new DestinationManager(HierarchyNamespaceOptions.None), check, create,
            limiter ?? new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, time), TimeSpan.FromSeconds(30), time);
    }

    static TransportOperations Publish(params Type[] types) =>
        new([.. types.Select(type => new TransportOperation(new OutgoingMessage(Guid.NewGuid().ToString(), [], ReadOnlyMemory<byte>.Empty),
            new MulticastAddressTag(type), [], DispatchConsistency.Default))]);

    static ServiceBusException Missing() => new("Missing", ServiceBusFailureReason.MessagingEntityNotFound);
    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    class RecordingDispatcher(Func<int, Task> onDispatch = null) : IMessageDispatcher
    {
        public int Calls { get; private set; }
        public Task Dispatch(TransportOperations operations, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            Calls++;
            return onDispatch?.Invoke(Calls) ?? Task.CompletedTask;
        }
    }

    class EventA;
    class EventB;
}