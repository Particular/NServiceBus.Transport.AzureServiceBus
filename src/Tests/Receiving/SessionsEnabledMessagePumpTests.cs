namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class SessionsEnabledMessagePumpTests
{
    [Test]
    public async Task Should_start_forwarders_for_subscribed_events_when_receiving_starts()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(clientFactory);
        var pump = CreatePump(subscriptionManager);

        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.Empty);

        await pump.StartReceive();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/receiveAddress" }));
    }

    static SessionsEnabledMessagePump CreatePump(SubscriptionManager subscriptionManager) =>
        new(new FakeSessionProcessorClient(), new AzureServiceBusTransport("connection-string", TopicTopology.Default) { EnableSessions = true }, "receiveAddress",
            new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), true, false, "error"), (_, _, _) => { },
            subscriptionManager);

    static TopicPerEventTopologySubscriptionManager CreateSubscriptionManager(FakeForwardingClientFactory clientFactory) =>
        TopicPerEventSubscriptionManagerForwardingTests.CreateSubscriptionManager(new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            }
        }, clientFactory, subscribingQueueName: "receiveAddress");

    class MyEvent1;

    class MyEvent2;
}