namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Logging;
using NServiceBus.Testing;
using NServiceBus.Transport.AzureServiceBus.Receiving;
using NUnit.Framework;

[TestFixture]
public class OrderedSubscriptionForwarderTests
{
    [Test, CancelAfter(10_000)]
    public async Task Should_release_processor_sender_and_client_when_subscription_does_not_exist(
        [Values(ServiceBusErrorSource.AcceptSession, ServiceBusErrorSource.Receive)] ServiceBusErrorSource errorSource,
        CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), errorSource, cancellationToken).WaitAsync(cancellationToken);

        var client = clientFactory.Clients.Single();
        Assert.That(() => client.DisposeCount, Is.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(client.SubscriptionProcessors.Single().WasStopped, Is.True);
            Assert.That(client.SubscriptionProcessors.Single().WasClosed, Is.True);
            Assert.That(client.Senders.Single().WasDisposed, Is.True);
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_warn_once_and_not_trigger_circuit_breaker_when_subscription_does_not_exist(CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var criticalErrorTriggered = false;
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // Other receive loops keep reporting the missing subscription until the processor has stopped
        var processor = clientFactory.Processors.Single();
        await processor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        await processor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        releaseStopProcessing.SetResult();
        await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(criticalErrorTriggered, Is.False);
            Assert.That(Regex.Matches(logOutput.ToString(), "Subscription 'Subscription1' on topic 'Topic1' does not exist, so its events cannot be forwarded to 'InputQueue'"), Has.Count.EqualTo(1));
            Assert.That(logOutput.ToString(), Does.Not.Contain("circuit breaker"));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_wait_for_the_processor_to_stop_in_the_error_handler(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        var raisingError = clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken);

        Assert.That(await Task.WhenAny(raisingError, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)), Is.SameAs(raisingError));

        releaseStopProcessing.SetResult();
        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_restart_with_a_new_client_when_started_after_subscription_did_not_exist(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[0].DisposeCount, Is.EqualTo(1));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_wait_for_resources_being_released_before_restarting(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

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
    public async Task Should_trigger_circuit_breaker_and_keep_running_when_forwarding_reports_not_found(CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // Sending to a missing input queue fails in the message callback, which does not mean the subscription is missing
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.ProcessMessageCallback, cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(criticalErrorTriggered, Is.True);
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
            Assert.That(clientFactory.Clients.Single().DisposeCount, Is.Zero);
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_keep_running_when_stopped_while_starting(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        // Start waits for the release of the previous resources, so Stop arrives before Start creates new ones
        var starting = forwarder.Start(cancellationToken);
        var stopping = forwarder.Stop(cancellationToken);

        Assert.That(clientFactory.Clients, Has.Count.EqualTo(1), "Start should still be waiting for the previous resources to be released");

        releaseStopProcessing.SetResult();
        await Task.WhenAll(starting, stopping).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(1), "The stopped start should not create new resources");
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
            Assert.That(clientFactory.Clients.SelectMany(client => client.Senders).Select(sender => sender.WasDisposed), Is.All.True);
        }
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
    public async Task Should_trigger_circuit_breaker_for_other_non_transient_errors(CancellationToken cancellationToken = default)
    {
        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // Awaiting the failure delays long enough for the immediately armed circuit breaker to trigger
        await clientFactory.Processors.Single().RaiseError(new ServiceBusException("general error", ServiceBusFailureReason.GeneralError), ServiceBusErrorSource.Receive, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(criticalErrorTriggered, Is.True);
    }

    public enum ForwarderState
    {
        NotStarted,
        Running,
        ReleasingResources,
        Dormant,
        Stopped
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_stop_and_dispose_in_every_state([Values] ForwarderState state, CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        releaseStopProcessing.SetResult();
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);

        if (state != ForwarderState.NotStarted)
        {
            await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        }

        if (state == ForwarderState.ReleasingResources)
        {
            releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            clientFactory.StopProcessing = () => releaseStopProcessing.Task;
        }

        if (state is ForwarderState.ReleasingResources or ForwarderState.Dormant)
        {
            await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        }

        if (state == ForwarderState.Dormant)
        {
            Assert.That(() => clientFactory.Clients.Single().DisposeCount, Is.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
        }

        if (state == ForwarderState.Stopped)
        {
            await forwarder.Stop(cancellationToken).WaitAsync(cancellationToken);
        }

        var stopping = forwarder.Stop(cancellationToken);

        if (state == ForwarderState.ReleasingResources)
        {
            Assert.That(await Task.WhenAny(stopping, Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)), Is.Not.SameAs(stopping), "Stop should wait for the resources being released");
            releaseStopProcessing.SetResult();
        }

        await stopping.WaitAsync(cancellationToken);
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
    public async Task Should_restart_and_keep_forwarding_when_not_found_arrives_after_the_subscription_was_confirmed(CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // Registering again while running, e.g. after a Subscribe provisioned the subscription
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        // Accepts issued before the subscription existed still report it as missing
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => clientFactory.Processors.Count(processor => processor.WasStarted), Is.EqualTo(2).After(5).Seconds.PollEvery(10).MilliSeconds);

        var restartedClient = clientFactory.Clients.Last();
        await restartedClient.SubscriptionProcessors.Single().ProcessMessage(ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId"), cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[0].DisposeCount, Is.EqualTo(1));
            Assert.That(restartedClient.DisposeCount, Is.Zero);
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription1" }));
            Assert.That(restartedClient.Senders.Single().IndividuallySentMessages, Has.Count.EqualTo(1));
            Assert.That(logOutput.ToString(), Does.Not.Contain("does not exist"));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_go_dormant_when_not_found_arrives_again_after_restarting_without_new_confirmation(CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        Assert.That(() => clientFactory.Processors.Count(processor => processor.WasStarted), Is.EqualTo(2).After(5).Seconds.PollEvery(10).MilliSeconds);

        await clientFactory.Processors.Last().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(Regex.Matches(logOutput.ToString(), "Subscription 'Subscription1' on topic 'Topic1' does not exist"), Has.Count.EqualTo(1));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_not_keep_running_when_stopped_while_restarting(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // The restart waits for the previous resources to be released, so Stop arrives before it creates new ones
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        var stopping = forwarder.Stop(cancellationToken);

        Assert.That(clientFactory.Clients, Has.Count.EqualTo(1), "The restart should still be waiting for the previous resources to be released");

        releaseStopProcessing.SetResult();
        await stopping.WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Processors.Where(processor => processor != clientFactory.Processors.First()).Select(processor => processor.WasStarted), Is.All.False);
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_start_once_when_registered_while_restarting(CancellationToken cancellationToken = default)
    {
        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        releaseStopProcessing.SetResult();

        Assert.That(() => clientFactory.RunningForwarders.Count(), Is.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[0].DisposeCount, Is.EqualTo(1));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);

        Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
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

    [Test, CancelAfter(10_000)]
    public async Task Should_start_processing_when_started_again_after_a_failed_start([Values] bool canceled, CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);

        if (canceled)
        {
            Assert.That(() => forwarder.Start(new CancellationToken(true)), Throws.InstanceOf<OperationCanceledException>());
        }
        else
        {
            clientFactory.StartProcessing = () => throw new InvalidOperationException("start failed");
            Assert.That(() => forwarder.Start(cancellationToken), Throws.InvalidOperationException);
            clientFactory.StartProcessing = () => Task.CompletedTask;
        }

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
    public async Task Should_ignore_a_late_not_found_from_a_run_that_is_being_stopped(CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Debug);
#pragma warning restore CS0618 // Type or member is obsolete

        var releaseStopProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientFactory = new FakeForwardingClientFactory { StopProcessing = () => releaseStopProcessing.Task };
        var forwarder = CreateForwarder(clientFactory);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        // The processor keeps reporting errors until it has stopped. The restart decision is taken inside the error handler,
        // so once it has returned, a wrongly scheduled restart is already visible in the log.
        var stopping = forwarder.Stop(cancellationToken);
        await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logOutput.ToString(), Does.Not.Contain("Restarting the forwarder"));
            Assert.That(logOutput.ToString(), Does.Not.Contain("does not exist"));
        }

        releaseStopProcessing.SetResult();
        await stopping.WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(1));
            Assert.That(clientFactory.Clients.Single().DisposeCount, Is.EqualTo(1));
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_restart_when_confirmed_while_the_processor_is_starting([Values] bool restarting, CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory);
        var startProcessingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStartProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> gatedStartProcessing = () =>
        {
            startProcessingEntered.TrySetResult();
            return releaseStartProcessing.Task;
        };

        Task starting = Task.CompletedTask;
        if (restarting)
        {
            await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
            await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
            clientFactory.StartProcessing = gatedStartProcessing;
            await clientFactory.Processors.Single().RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        }
        else
        {
            clientFactory.StartProcessing = gatedStartProcessing;
            starting = forwarder.Start(cancellationToken);
        }

        // The SDK issues accepts before StartProcessingAsync returns, so a confirmation can arrive in between
        await startProcessingEntered.Task.WaitAsync(cancellationToken);
        var startingProcessor = clientFactory.Processors.Last();
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        clientFactory.StartProcessing = () => Task.CompletedTask;
        releaseStartProcessing.SetResult();
        await starting.WaitAsync(cancellationToken);
        Assert.That(() => startingProcessor.IsActive, Is.True.After(5).Seconds.PollEvery(10).MilliSeconds);

        // A stale not-found from accepts issued before the subscription existed
        await startingProcessor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => clientFactory.Processors.Last() != startingProcessor && clientFactory.Processors.Last().IsActive, Is.True.After(5).Seconds.PollEvery(10).MilliSeconds, "The confirmed run should restart instead of going dormant");
        Assert.That(logOutput.ToString(), Does.Not.Contain("does not exist"));

        // The restarted run has no confirmation of its own yet
        var restartedProcessor = clientFactory.Processors.Last();
        await restartedProcessor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);

        Assert.That(() => clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1).After(5).Seconds.PollEvery(10).MilliSeconds);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Processors.Last(), Is.SameAs(restartedProcessor));
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(Regex.Matches(logOutput.ToString(), "Subscription 'Subscription1' on topic 'Topic1' does not exist"), Has.Count.EqualTo(1));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_ignore_late_errors_from_a_previous_run(CancellationToken cancellationToken = default)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var criticalErrorTriggered = false;
        var clientFactory = new FakeForwardingClientFactory();
        var forwarder = CreateForwarder(clientFactory, () => criticalErrorTriggered = true);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);
        var previousProcessor = clientFactory.Processors.Single();
        await previousProcessor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        await forwarder.Start(cancellationToken).WaitAsync(cancellationToken);

        await previousProcessor.RaiseError(SubscriptionNotFound(), ServiceBusErrorSource.AcceptSession, cancellationToken).WaitAsync(cancellationToken);
        await previousProcessor.RaiseError(new ServiceBusException("general error", ServiceBusFailureReason.GeneralError), ServiceBusErrorSource.Receive, cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(criticalErrorTriggered, Is.False);
            Assert.That(clientFactory.Clients, Has.Count.EqualTo(2));
            Assert.That(clientFactory.Clients[1].DisposeCount, Is.Zero);
            Assert.That(clientFactory.Processors.Last().IsActive, Is.True);
            Assert.That(Regex.Matches(logOutput.ToString(), "does not exist"), Has.Count.EqualTo(1));
        }

        await forwarder.DisposeAsync().AsTask().WaitAsync(cancellationToken);
    }

    static OrderedSubscriptionForwarder CreateForwarder(FakeForwardingClientFactory clientFactory, Action criticalErrorAction = null) =>
        new(clientFactory.Create, "Topic1", "Subscription1", "InputQueue", TimeSpan.Zero, (_, _, _) => criticalErrorAction?.Invoke());

    static ServiceBusException SubscriptionNotFound() => new("not found", ServiceBusFailureReason.MessagingEntityNotFound);
}