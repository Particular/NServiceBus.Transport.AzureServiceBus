namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Transport.AzureServiceBus.Receiving;
using NUnit.Framework;

[TestFixture]
public class OrderedSubscriptionForwarderTests
{
    [Test, CancelAfter(10_000)]
    public async Task Should_trigger_circuit_breaker_and_keep_running_when_subscription_does_not_exist(
        [Values(ServiceBusErrorSource.AcceptSession, ServiceBusErrorSource.Receive, ServiceBusErrorSource.ProcessMessageCallback)] ServiceBusErrorSource errorSource,
        CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // Awaiting the failure delays long enough for the immediately armed circuit breaker to trigger
        await clientFactory.Processors.Single().RaiseError(new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound), errorSource, cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(criticalErrorTriggered, Is.True);
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
            Assert.That(clientFactory.Clients.Single().DisposeCount, Is.Zero);
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_trigger_circuit_breaker_for_other_non_transient_errors(CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await clientFactory.Processors.Single().RaiseError(new ServiceBusException("general error", ServiceBusFailureReason.GeneralError), ServiceBusErrorSource.Receive, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(criticalErrorTriggered, Is.True);

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_trigger_circuit_breaker_for_transient_errors(CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await clientFactory.Processors.Single().RaiseError(new ServiceBusException("busy", ServiceBusFailureReason.ServiceBusy), ServiceBusErrorSource.Receive, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(criticalErrorTriggered, Is.False);

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_once_when_started_again_while_running(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);

        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(1));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_processing_when_started_again_after_a_failed_start([Values] bool canceled, CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory
        {
            StartProcessing = () => canceled ? throw new OperationCanceledException() : throw new InvalidOperationException("start failed")
        };
        var forwarder = CreateForwarder(clientFactory);

        Assert.That(() => forwarder.Start(cancellationToken), canceled ? Throws.InstanceOf<OperationCanceledException>() : Throws.InvalidOperationException);
        clientFactory.StartProcessing = () => Task.CompletedTask;

        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[0].DisposeCount, Is.EqualTo(1), "The failed start should release what it created");
            Assert.That(clientFactory.Clients[0].Senders.Single().WasDisposed, Is.True);
            Assert.That(clientFactory.Processors.Last().IsActive, Is.True);
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_wait_for_resources_being_released_before_starting_again(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        using var stopCancellation = new CancellationTokenSource();
        var stopping = forwarder.Stop(stopCancellation.Token);
        await stopCancellation.CancelAsync();
        Assert.That(() => stopping, Throws.InstanceOf<OperationCanceledException>());

        var starting = forwarder.Start(cancellationToken);

        Assert.That(await Task.WhenAny(starting, Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)), Is.Not.SameAs(starting), "Start should wait for the resources being released");

        releaseStopProcessing.SetResult();
        await starting.WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[0].DisposeCount, Is.EqualTo(1));
            Assert.That(clientFactory.Clients[1].DisposeCount, Is.Zero);
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);

        Assert.That(clientFactory.Clients[1].DisposeCount, Is.EqualTo(1));
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_dispose_sender_and_client_when_disposing_the_processor_fails(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory { CloseProcessor = () => throw new InvalidOperationException("close failed") };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients.Single().Senders.Single().WasDisposed, Is.True);
            Assert.That(clientFactory.Clients.Single().DisposeCount, Is.EqualTo(1));
        }

        clientFactory.CloseProcessor = () => Task.CompletedTask;
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }), "The state should be cleared so the forwarder can start again");

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_complete_stop_and_dispose_when_stopping_the_processor_fails(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => throw new InvalidOperationException("stop failed") };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);

        var client = clientFactory.Clients.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(client.Senders.Single().WasDisposed, Is.True);
            Assert.That(client.DisposeCount, Is.EqualTo(1));
        }
    }

    public enum ForwarderState
    {
        NotStarted,
        Running,
        Stopped
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_stop_and_dispose_in_every_state([Values] ForwarderState state, CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);

        if (state != ForwarderState.NotStarted)
        {
            await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        }

        if (state == ForwarderState.Stopped)
        {
            await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        }

        await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
            Assert.That(clientFactory.Clients.SelectMany(client => client.Senders).Select(sender => sender.WasDisposed), Is.All.True);
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(state == ForwarderState.NotStarted ? 0 : 1));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_release_everything_when_stopped_again_after_a_canceled_stop([Values] bool dispose, CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => forwarder.Stop(new CancellationToken(true)), Throws.InstanceOf<OperationCanceledException>());

        releaseStopProcessing.SetResult();
        if (dispose)
        {
            await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
        }
        else
        {
            await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        }

        var client = clientFactory.Clients.Single();
        var processor = client.SubscriptionProcessors.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processor.IsActive, Is.False);
            Assert.That(processor.WasClosed, Is.True);
            Assert.That(client.Senders.Single().WasDisposed, Is.True);
            Assert.That(client.DisposeCount, Is.EqualTo(1));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_ignore_late_errors_from_a_stopped_processor(CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        var previousProcessor = clientFactory.Processors.Single();
        await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await previousProcessor.RaiseError(new ServiceBusException("general error", ServiceBusFailureReason.GeneralError), ServiceBusErrorSource.Receive, cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(criticalErrorTriggered, Is.False);
            Assert.That(clientFactory.Processors.Last().IsActive, Is.True);
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    static OrderedSubscriptionForwarder CreateForwarder(FakeForwardingClientFactory clientFactory, Action criticalErrorAction = null) =>
        new(clientFactory.Create, "Topic1", "Subscription1", "InputQueue", TimeSpan.Zero, (_, _, _) => criticalErrorAction?.Invoke());
}
