namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;

[TestFixture]
public class AzureServiceBusTransportInfrastructureTests
{
    [Test, CancelAfter(10_000)]
    public async Task Should_shut_down_forwarders_on_shutdown(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var infrastructure = await CreateStartedInfrastructure(new FakeSessionProcessorClient(), clientFactory, cancellationToken);

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/receiveAddress", "Topic2/receiveAddress" }));

        await infrastructure.Shutdown(cancellationToken).WaitAsync(cancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
            Assert.That(clientFactory.Clients.SelectMany(client => client.Senders).Select(sender => sender.WasDisposed), Is.All.True);
        }
    }

    [Test, CancelAfter(10_000)]
    public async Task Should_shut_down_forwarders_when_stopping_the_receivers_fails(CancellationToken cancellationToken = default)
    {
        var clientFactory = new FakeForwardingClientFactory();
        var receiveClient = new FakeSessionProcessorClient { StopProcessing = () => throw new InvalidOperationException("stop failed") };
        var infrastructure = await CreateStartedInfrastructure(receiveClient, clientFactory, cancellationToken);

        Assert.That(() => infrastructure.Shutdown(cancellationToken), Throws.Exception);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(clientFactory.Clients.Select(client => client.DisposeCount), Is.All.EqualTo(1));
        }
    }

    // Raw mode, so the forwarders are started from the topology mapping
    static async Task<AzureServiceBusTransportInfrastructure> CreateStartedInfrastructure(FakeSessionProcessorClient receiveClient, FakeForwardingClientFactory clientFactory, CancellationToken cancellationToken)
    {
        var topology = TopicTopology.Default;
        topology.SubscribeTo<MyEvent1>("Topic1");
        topology.SubscribeTo<MyEvent2>("Topic2");
        var transport = new AzureServiceBusTransport("connection-string", topology) { EnableSessions = true };
        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, false);
        var receiveSettings = new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), true, false, "error");

        var infrastructure = new AzureServiceBusTransportInfrastructure(transport, hostSettings, [(receiveSettings, receiveClient)], receiveClient,
            clientFactory.Create, new RecordingServiceBusAdministrationClient(), new DestinationManager(HierarchyNamespaceOptions.None));
        var pump = infrastructure.Receivers["TestReceiver"];

        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), cancellationToken);
        await pump.StartReceive(cancellationToken);

        return infrastructure;
    }

    class MyEvent1;

    class MyEvent2;
}
