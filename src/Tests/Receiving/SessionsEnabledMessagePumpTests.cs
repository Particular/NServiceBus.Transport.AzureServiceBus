namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Extensibility;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class SessionsEnabledMessagePumpTests
{
    [Test]
    public async Task Should_start_forwarders_from_topology_mapping_when_not_subscribed()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var pump = CreatePump(CreateSubscriptionManager(clientFactory));

        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
        await pump.StartReceive();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/receiveAddress", "Topic2/receiveAddress" }));
    }

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

    [Test]
    public async Task Should_not_use_topology_mapping_when_subscribing_failed()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(clientFactory, new TopicPerEventSubscriptionManagerForwardingTests.FailingAdministrationClient());
        var pump = CreatePump(subscriptionManager);

        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
        Assert.ThrowsAsync<ServiceBusException>(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag()));
        await pump.StartReceive();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/receiveAddress" }));
    }

    [Test]
    public async Task Should_start_forwarders_from_topology_mapping_in_raw_mode()
    {
        var topology = TopicTopology.Default;
        topology.SubscribeTo<MyEvent1>("Topic1");
        var transport = new AzureServiceBusTransport("connection-string", topology) { EnableSessions = true };
        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, false);
        var client = new FakeSessionProcessorClient();
        var clientFactory = new FakeForwardingClientFactory();
        var receiveSettings = new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), true, false, "error");

        var infrastructure = new AzureServiceBusTransportInfrastructure(transport, hostSettings, [(receiveSettings, client)], client,
            clientFactory.Create, new RecordingServiceBusAdministrationClient(), new DestinationManager(HierarchyNamespaceOptions.None));
        var pump = infrastructure.Receivers["TestReceiver"];

        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
        await pump.StartReceive();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/receiveAddress" }));
    }

    static SessionsEnabledMessagePump CreatePump(SubscriptionManager subscriptionManager) =>
        new(new FakeSessionProcessorClient(), new AzureServiceBusTransport("connection-string", TopicTopology.Default) { EnableSessions = true }, "receiveAddress",
            new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), true, false, "error"), (_, _, _) => { },
            subscriptionManager);

    static TopicPerEventTopologySubscriptionManager CreateSubscriptionManager(FakeForwardingClientFactory clientFactory, ServiceBusAdministrationClient administrationClient = null) =>
        TopicPerEventSubscriptionManagerForwardingTests.CreateSubscriptionManager(new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            }
        }, clientFactory, administrationClient, subscribingQueueName: "receiveAddress");

    class MyEvent1;

    class MyEvent2;
}