namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Text;
using System.Threading.Tasks;
using EventRouting;
using Extensibility;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
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

    [Test]
    public void Should_throw_when_incompatible_filter_modes_for_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("SharedTopic", SubscriptionFilterMode.CorrelationFilter)] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", SubscriptionFilterMode.SqlFilter)] }
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

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));

        Assert.That(exception.Message, Does.Contain("Incompatible subscription filter modes"));
    }

    [Test]
    public async Task Should_create_subscription_with_correlation_filter_rule()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", SubscriptionFilterMode.CorrelationFilter)] }
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_create_subscription_with_sql_filter_rule()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", SubscriptionFilterMode.SqlFilter)] }
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_support_hierarchy_namespace_with_correlation_filter()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var destinationManager = new DestinationManager(hierarchyOptions);
        var queueName = destinationManager.GetDestination("SubscribingQueue");
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry(destinationManager.GetDestination("MyTopic"), SubscriptionFilterMode.CorrelationFilter)] }
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_apply_hierarchy_namespace_to_fluent_subscription_mappings()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyNamespaceOptions = hierarchyOptions,
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "my-hierarchy/MySubscriptionName" } },
        };

        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.FilterMode = SubscriptionFilterMode.CorrelationFilter);

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "my-hierarchy/SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_unsubscribe_fluent_subscription_mapping_using_hierarchy_adjusted_topic_name()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyNamespaceOptions = hierarchyOptions,
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "my-hierarchy/MySubscriptionName" } },
        };

        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.FilterMode = SubscriptionFilterMode.CorrelationFilter);

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "my-hierarchy/SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteRule(topicName: 'my-hierarchy/MyTopic', subscriptionName: 'MySubscriptionName'"));
    }

    [Test]
    public async Task Should_apply_subscription_name_override_for_non_namespaced_queue_when_hierarchy_enabled()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyNamespaceOptions = hierarchyOptions,
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", SubscriptionFilterMode.CorrelationFilter)] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "my-hierarchy/SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("\"SubscriptionName\": \"MySubscriptionName\""));
    }

    [Test]
    public async Task Should_use_topology_default_subscription_filter_mode_for_unconfigured_subscription()
    {
        var topologyOptions = new TopologyOptions
        {
            DefaultSubscriptionFilterMode = SubscriptionFilterMode.CorrelationFilter,
            SubscribedEventToTopicsMap = { { typeof(MyEvent1).FullName, ["MyTopic"] } }
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("CreateRuleOptions(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue')"));
    }

    [Test]
    public async Task Should_create_filtered_subscription_for_acceptance_style_hierarchy_configuration()
    {
        const string endpointName = "NServiceBus.AcceptanceTests.NativePubSub.When_using_topic_per_event_topology_with_hierarchy_and_correlation_filter_multiplexing+Subscriber";
        const string shortenedSubscriptionName = "subscriber-short";
        const string sharedTopicName = "HierarchyCorrelationFilterMultiplexing";

        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyNamespaceOptions = hierarchyOptions,
            SubscribedEventToTopicsMap =
            {
                { typeof(IMyEvent).FullName, [new SubscriptionEntry(sharedTopicName, SubscriptionFilterMode.CorrelationFilter)] }
            },
            QueueNameToSubscriptionNameMap = { { endpointName, shortenedSubscriptionName } },
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = $"my-hierarchy/{endpointName}",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(IMyEvent))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    class MyEvent1;
    class MyEvent2;
    interface IMyEvent;
}
