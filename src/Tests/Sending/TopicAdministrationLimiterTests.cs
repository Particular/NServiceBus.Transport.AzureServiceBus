namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

[TestFixture]
public class TopicAdministrationLimiterTests
{
    [Test]
    public async Task Should_delay_first_admission_and_space_subsequent_starts()
    {
        var time = new FakeTimeProvider();
        var limiter = new TopicAdministrationLimiter(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500), time);
        var starts = new List<long>();
        Task<bool> Operation(CancellationToken _)
        { starts.Add(time.GetTimestamp()); return Task.FromResult(true); }
        var epoch = time.GetTimestamp();

        var first = limiter.ExecuteAsync(Operation);
        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.That(starts, Is.Empty);
        time.Advance(TimeSpan.FromMilliseconds(1));
        await first;

        var second = limiter.ExecuteAsync(Operation);
        time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.That(starts, Has.Count.EqualTo(1));
        time.Advance(TimeSpan.FromMilliseconds(1));
        await second;

        var third = limiter.ExecuteAsync(Operation);
        time.Advance(TimeSpan.FromMilliseconds(200));
        await third;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(time.GetElapsedTime(epoch, starts[0]), Is.EqualTo(TimeSpan.FromMilliseconds(500)));
            Assert.That(time.GetElapsedTime(starts[0], starts[1]), Is.EqualTo(TimeSpan.FromMilliseconds(200)));
            Assert.That(time.GetElapsedTime(starts[1], starts[2]), Is.EqualTo(TimeSpan.FromMilliseconds(200)));
        }
    }

    [Test]
    public async Task Should_keep_only_one_sdk_call_in_flight()
    {
        var time = new FakeTimeProvider();
        var limiter = new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, time);
        var release = Signal();
        var started = 0;
        var first = limiter.ExecuteAsync(_ => { started++; return release.Task; });
        var second = limiter.ExecuteAsync(_ => { started++; return Task.CompletedTask; });

        time.Advance(TimeSpan.FromMinutes(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.EqualTo(1));
            Assert.That(second.IsCompleted, Is.False);
        }
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.That(started, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_cancel_a_waiting_operation_without_invoking_it()
    {
        var limiter = new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, new FakeTimeProvider());
        var release = Signal();
        var first = limiter.ExecuteAsync(_ => release.Task);
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var cancelled = limiter.ExecuteAsync(_ => { calls++; return Task.CompletedTask; }, cancellation.Token);
        await cancellation.CancelAsync();
        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());

        var next = limiter.ExecuteAsync(_ => { calls++; return Task.CompletedTask; });
        release.SetResult();
        await Task.WhenAll(first, next);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_retain_the_slot_if_a_caller_stops_waiting_for_an_unresponsive_sdk_call()
    {
        var limiter = new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, new FakeTimeProvider());
        var release = Signal();
        using var cancellation = new CancellationTokenSource();
        var first = limiter.ExecuteAsync(_ => release.Task, cancellation.Token);
        var nextCalls = 0;
        var next = limiter.ExecuteAsync(_ => { nextCalls++; return Task.CompletedTask; });

        await cancellation.CancelAsync();
        Assert.That(async () => await first.WaitAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(nextCalls, Is.Zero);
        }

        release.SetResult();
        await Task.WhenAll(first, next);
        Assert.That(nextCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_release_admission_after_a_failed_sdk_call()
    {
        var limiter = new TopicAdministrationLimiter(TimeSpan.Zero, TimeSpan.Zero, new FakeTimeProvider());
        Assert.ThrowsAsync<InvalidOperationException>(() => limiter.ExecuteAsync(static _ => Task.FromException(new InvalidOperationException("Failed"))));
        var calls = 0;
        await limiter.ExecuteAsync(_ => { calls++; return Task.CompletedTask; });
        Assert.That(calls, Is.EqualTo(1));
    }

    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}