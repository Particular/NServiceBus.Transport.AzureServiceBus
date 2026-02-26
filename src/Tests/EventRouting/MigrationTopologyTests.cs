namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventRouting;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class MigrationTopologyTests
{
    [Test]
    public void Should_self_validate()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = new string('a', 261),
            TopicToSubscribeOn = new string('a', 261),
            PublishedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, new string('c', 261) },
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] },
                { typeof(MyEventMappedTwice).FullName, ["MyEventMappedTwice"] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, new string('g', 51) } },
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    [Test]
    public void Should_self_validate_consistency()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = "TopicToPublishTo",
            TopicToSubscribeOn = "TopicToSubscribeOn",
            PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, "TopicToPublishTo" } },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, ["SomeOtherTopic", "TopicToSubscribeOn"] },
                { typeof(MyEventMappedTwice).FullName, ["MyEventMappedTwice"] }
            },
            EventsToMigrateMap = { typeof(MyEventMappedTwice).FullName }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        ValidationException validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    // With the generic host validation can already be done at startup and this allows disabling further validation
    // for advanced scenarios to save startup time.
    [Test]
    public void Should_allow_disabling_validation()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = new string('a', 261),
            TopicToSubscribeOn = new string('a', 261)
        };

        var topology = TopicTopology.FromOptions(topologyOptions);
        topology.OptionsValidator = new TopologyOptionsDisableValidationValidator();

        Assert.DoesNotThrow(() => topology.Validate());
    }

    [Test]
    public async Task Should_set_MaxDeliveryCount_to_max_int_when_not_using_emulator()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topology = TopicTopology.MigrateFromSingleDefaultTopic();
        topology.OverrideSubscriptionNameFor("SubscribingQueue", "MySub");

        var transport = new AzureServiceBusTransport("connectionString", topology);
#pragma warning restore CS0618 // Type or member is obsolete

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
        var subscriptionManager = (SubscriptionManager)messagePump.Subscriptions!;

        await subscriptionManager.SetupInfrastructureIfNecessary(CancellationToken.None);

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_set_MaxDeliveryCount_to_10_when_using_emulator()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topology = TopicTopology.MigrateFromSingleDefaultTopic();
        topology.OverrideSubscriptionNameFor("SubscribingQueue", "MySub");

        var transport = new AzureServiceBusTransport("UseDevelopmentEmulator=true", topology);
#pragma warning restore CS0618 // Type or member is obsolete

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
        var subscriptionManager = (SubscriptionManager)messagePump.Subscriptions!;

        await subscriptionManager.SetupInfrastructureIfNecessary(CancellationToken.None);

        Approver.Verify(builder.ToString());
    }

    class MyEvent;
    class MyEventMappedTwice;
}