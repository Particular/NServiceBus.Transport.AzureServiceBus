namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Threading.Tasks;
using EventRouting;
using Extensibility;
using NUnit.Framework;
using Particular.Approvals;
using Unicast.Messages;

[TestFixture]
public class TopicPerEventSubscriptionManagerTests
{
    [Test]
    public async Task Should_create_topology_for_mapped_events()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["MyTopic1", "MyTopic2"] },
                { typeof(MyEvent2).FullName, ["MyTopic3"] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_create_topology_for_unmapped_events()
    {
        var topologyOptions = new TopologyOptions
        {
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_strip_hierarchy_namespace_from_subscription_names()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var destinationManager = new DestinationManager(hierarchyOptions);
        var queueName = destinationManager.GetDestination("SubscribingQueue");
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [ destinationManager.GetDestination("MyTopic1"), destinationManager.GetDestination("MyTopic2")] }
            },
            QueueNameToSubscriptionNameMap = { { queueName, destinationManager.GetDestination("MySubscriptionName") } },
            HierarchyNamespaceOptions = hierarchyOptions,
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = queueName,
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    class MyEvent1;
    class MyEvent2;
}