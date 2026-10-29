namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NUnit.Framework;

[TestFixture]
public class SubscriptionForwardersTests
{
    [Test, CancelAfter(10_000)]
    public async Task Should_only_record_forwarders_until_started(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("Topic2", "Subscription", "Event2", cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.Empty);

        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription", "Topic2/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_forwarder_immediately_when_registered_after_started(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_forwarder_registered_while_starting(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);

        var startProcessingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStartProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientFactory.StartProcessing = () =>
        {
            startProcessingEntered.TrySetResult();
            return releaseStartProcessing.Task;
        };

        var starting = forwarders.Start(cancellationToken);
        await startProcessingEntered.Task.WaitAsync(cancellationToken);
        var registering = forwarders.Register("Topic2", "Subscription", "Event2", cancellationToken);

        Assert.That(registering.IsCompleted, Is.False);

        clientFactory.StartProcessing = () => Task.CompletedTask;
        releaseStartProcessing.SetResult();
        await Task.WhenAll(starting, registering).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription", "Topic2/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_start_forwarder_unregistered_before_started(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Unregister("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.Processors, Is.Empty);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_restart_forwarder_registered_again_after_its_subscription_did_not_exist(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);
        await GoDormant(clientFactory, cancellationToken);

        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_restart_forwarder_when_started_again_after_its_subscription_did_not_exist(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);
        await GoDormant(clientFactory, cancellationToken);

        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_keep_registrations_of_forwarder_whose_subscription_did_not_exist(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("SharedTopic", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("SharedTopic", "Subscription", "Event2", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);
        await GoDormant(clientFactory, cancellationToken);

        await forwarders.Unregister("SharedTopic", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("SharedTopic", "Subscription", "Event3", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Unregister("SharedTopic", "Subscription", "Event3", cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "SharedTopic/Subscription" }));

        await forwarders.Unregister("SharedTopic", "Subscription", "Event2", cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_keep_forwarding_when_a_stale_not_found_arrives_after_subscribing(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        // Started from the topology mapping before the subscription exists
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        // An explicit Subscribe provisions the subscription and registers the event again
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Clients.Single().SubscriptionProcessors.Single().RaiseError(new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => clientFactory.Processors.Count(processor => processor.WasStarted), Is.EqualTo(2).After(5).Seconds.PollEvery(10).MilliSeconds);
        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription" }));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_dispose_all_forwarders_when_shutdown_is_canceled(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("Topic2", "Subscription", "Event2", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        var shuttingDown = forwarders.Shutdown(new CancellationToken(true));
        releaseStopProcessing.SetResult();
        await shuttingDown.WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Processors.Select(processor => processor.IsActive), Is.All.False);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
            Assert.That(clientFactory.Clients.SelectMany(client => client.Senders).Select(sender => sender.WasDisposed), Is.All.True);
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_processing_when_registered_again_after_a_failed_start(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        clientFactory.StartProcessing = () => throw new InvalidOperationException("start failed");
        Assert.That(() => forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken), Throws.InvalidOperationException);
        clientFactory.StartProcessing = () => Task.CompletedTask;

        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.Processors.Last().IsActive, Is.True);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_stop_started_forwarders_when_a_forwarder_fails_to_start(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Register("Topic2", "Subscription", "Event2", cancellationToken).WaitAsync(cancellationToken);

        var starts = 0;
        clientFactory.StartProcessing = () => ++starts == 2 ? throw new InvalidOperationException("start failed") : Task.CompletedTask;

        Assert.That(() => forwarders.Start(cancellationToken), Throws.InvalidOperationException);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
        }

        // Not started anymore, so a registration is only recorded again
        await forwarders.Register("Topic3", "Subscription", "Event3", cancellationToken).WaitAsync(cancellationToken);
        Assert.That(clientFactory.RunningForwarders, Is.Empty);

        clientFactory.StartProcessing = () => Task.CompletedTask;
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription", "Topic2/Subscription", "Topic3/Subscription" }));
    }

    static async Task GoDormant(FakeForwardingClientFactory clientFactory, CancellationToken cancellationToken)
    {
        var client = clientFactory.Clients.Last();
        await client.SubscriptionProcessors.Single().RaiseError(new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        Assert.That(() => client.DisposeCount, Is.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_trigger_the_circuit_breaker_of_an_unregistered_forwarder(CancellationToken cancellationToken = default)
    {
        var criticalErrors = 0;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = new SubscriptionForwarders(clientFactory.Create, "InputQueue", TimeSpan.FromMilliseconds(100), (_, _, _) => Interlocked.Increment(ref criticalErrors));
        await forwarders.Register("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await forwarders.Start(cancellationToken).WaitAsync(cancellationToken);

        // Arms the circuit breaker, which then delays the error handler
        var raisingError = clientFactory.Processors.Single().RaiseError(new ServiceBusException("failed", ServiceBusFailureReason.GeneralError), ServiceBusErrorSource.Receive, cancellationToken);

        await forwarders.Unregister("Topic1", "Subscription", "Event1", cancellationToken).WaitAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        Assert.That(Volatile.Read(ref criticalErrors), Is.Zero);

        await raisingError.WaitAsync(cancellationToken);
    }

    static SubscriptionForwarders CreateForwarders(FakeForwardingClientFactory clientFactory) =>
        new(clientFactory.Create, "InputQueue", TimeSpan.FromMinutes(2), (_, _, _) => { });
}