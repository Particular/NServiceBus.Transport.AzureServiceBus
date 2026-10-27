namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class SubscriptionForwardersTests
{
    [Test]
    public async Task Should_only_record_forwarders_until_started()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Register("Topic1", "Subscription", "Event1");
        await forwarders.Register("Topic2", "Subscription", "Event2");

        Assert.That(clientFactory.RunningForwarders, Is.Empty);

        await forwarders.Start();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription", "Topic2/Subscription" }));
    }

    [Test]
    public async Task Should_start_forwarder_immediately_when_registered_after_started()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Start();
        await forwarders.Register("Topic1", "Subscription", "Event1");

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription" }));
    }

    [Test]
    public async Task Should_start_forwarder_registered_while_starting()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);
        await forwarders.Register("Topic1", "Subscription", "Event1");

        var startProcessingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStartProcessing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clientFactory.StartProcessing = () =>
        {
            startProcessingEntered.TrySetResult();
            return releaseStartProcessing.Task;
        };

        var starting = forwarders.Start();
        await startProcessingEntered.Task;
        var registering = forwarders.Register("Topic2", "Subscription", "Event2");

        Assert.That(registering.IsCompleted, Is.False);

        clientFactory.StartProcessing = () => Task.CompletedTask;
        releaseStartProcessing.SetResult();
        await Task.WhenAll(starting, registering);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/Subscription", "Topic2/Subscription" }));
    }

    [Test]
    public async Task Should_not_start_forwarder_unregistered_before_started()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var forwarders = CreateForwarders(clientFactory);

        await forwarders.Register("Topic1", "Subscription", "Event1");
        await forwarders.Unregister("Topic1", "Subscription", "Event1");
        await forwarders.Start();

        Assert.That(clientFactory.Processors, Is.Empty);
    }

    static SubscriptionForwarders CreateForwarders(FakeForwardingClientFactory clientFactory) =>
        new(clientFactory.Create, "InputQueue", TimeSpan.FromMinutes(2), (_, _, _) => { });
}