namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using EventRouting;
using NUnit.Framework;
using Particular.Approvals;
using Unicast.Messages;

[TestFixture]
public class TopicPerEventTopologyTests
{
    [Test]
    public void PublishDestination_Should_return_mapped_topic_when_event_is_mapped()
    {
        var topologyOptions = new TopologyOptions
        {
            PublishedEventToTopicsMap =
            {
                [typeof(MyEvent).FullName] = "MyTopic"
            }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetPublishDestination(typeof(MyEvent));

        Assert.That(result, Is.EqualTo("MyTopic"));
    }

    [Test]
    public void PublishDestination_Should_default_topic_to_event_name()
    {
        var topologyOptions = new TopologyOptions();

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetPublishDestination(typeof(MyEvent));

        Assert.That(result, Is.EqualTo(typeof(MyEvent).FullName));
    }

    [Test]
    public void PublishDestination_Should_throw_when_instructed_to_and_type_unmapped()
    {
        var topologyOptions = new TopologyOptions
        {
            ThrowIfUnmappedEventTypes = true
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        Assert.Throws<Exception>(() => topology.GetPublishDestination(typeof(MyEvent)));
    }

    [Test]
    public void Should_self_validate()
    {
        var topologyOptions = new TopologyOptions
        {
            PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, new string('c', 261) } },
            SubscribedEventToTopicsMap = { { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] } },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    // With the generic host validation can already be done at startup and this allows disabling further validation
    // for advanced scenarios to save startup time.
    [Test]
    public void Should_allow_disabling_validation()
    {
        var topologyOptions = new TopologyOptions
        {
            PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, new string('c', 261) } }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);
        topology.OptionsValidator = new TopologyOptionsDisableValidationValidator();

        Assert.DoesNotThrow(() => topology.Validate());
    }

    [Test]
    public async Task Should_set_MaxDeliveryCount_to_max_int_when_not_using_emulator()
    {
        var topologyOptions = new TopologyOptions();
        var topology = TopicTopology.FromOptions(topologyOptions);
        var transport = new AzureServiceBusTransport("connectionString", topology);

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, true);
        var receiveSettings = new ReceiveSettings("TestReceiver", new QueueAddress("SubscribingQueue"), true, false, "error");
        var destinationManager = new DestinationManager(HierarchyNamespaceOptions.None);

        var infrastructure = new AzureServiceBusTransportInfrastructure(
            transport,
            hostSettings,
            [(receiveSettings, client)],
            client,
            administrationClient,
            destinationManager);

        var messagePump = (MessagePump)infrastructure.Receivers["TestReceiver"];
        var subscriptionManager = messagePump.Subscriptions!;

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new Extensibility.ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_set_MaxDeliveryCount_to_10_when_using_emulator()
    {
        var topologyOptions = new TopologyOptions();
        var topology = TopicTopology.FromOptions(topologyOptions);
        var transport = new AzureServiceBusTransport("UseDevelopmentEmulator=true", topology);

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var hostSettings = new HostSettings("endpoint", "host", new StartupDiagnosticEntries(), (_, _, _) => { }, true);
        var receiveSettings = new ReceiveSettings("TestReceiver", new QueueAddress("SubscribingQueue"), true, false, "error");
        var destinationManager = new DestinationManager(HierarchyNamespaceOptions.None);

        var infrastructure = new AzureServiceBusTransportInfrastructure(
            transport,
            hostSettings,
            [(receiveSettings, client)],
            client,
            administrationClient,
            destinationManager);

        var messagePump = (MessagePump)infrastructure.Receivers["TestReceiver"];
        var subscriptionManager = messagePump.Subscriptions!;

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new Extensibility.ContextBag());

        Approver.Verify(builder.ToString());
    }

    class MyEvent;
}