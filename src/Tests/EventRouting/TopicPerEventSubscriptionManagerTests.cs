namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Text;
using System.Threading.Tasks;
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
    public async Task Should_apply_subscription_name_unaffected_by_hierarchy_namespace()
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
            HierarchyOptions = hierarchyOptions,
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
    public void Should_throw_when_incompatible_routing_modes_for_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.CorrelationFilter)] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.SqlLikeFilter)] }
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

        Assert.That(exception.Message, Does.Contain("Incompatible subscription routing modes"));
    }

    [Test]
    public void Should_allow_default_and_correlation_filter_routing_modes_on_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["SharedTopic"] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.CorrelationFilter)] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
    }

    [Test]
    public void Should_throw_when_not_multiplexed_and_sql_filter_routing_modes_target_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.NotMultiplexed)] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.SqlLikeFilter)] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));

        Assert.That(exception!.Message, Does.Contain("Incompatible subscription routing modes"));
    }

    [Test]
    public void Should_allow_not_multiplexed_and_correlation_filter_routing_modes_on_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.NotMultiplexed)] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.CorrelationFilter)] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
    }

    [Test]
    public void Should_allow_mapped_and_unmapped_events_to_share_topic_with_correlation_filter_fallback_routing()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["SharedTopic"] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
    }

    [Test]
    public async Task Should_create_subscription_with_correlation_filter_rule()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", TopicRoutingMode.CorrelationFilter)] }
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
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", TopicRoutingMode.SqlLikeFilter)] }
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
    public async Task Should_create_catch_all_subscription_for_explicit_not_multiplexed_routing_mode()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", TopicRoutingMode.NotMultiplexed)] }
            }
        };

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("\"TopicName\": \"MyTopic\""));
        Assert.That(builder.ToString(), Does.Not.Contain("CreateRuleOptions(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue')"));
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
                { typeof(MyEvent1).FullName, [new SubscriptionEntry(destinationManager.GetDestination("MyTopic"), TopicRoutingMode.CorrelationFilter)] }
            },
            QueueNameToSubscriptionNameMap = { { queueName, destinationManager.GetDestination("MySubscriptionName") } },
            HierarchyOptions = hierarchyOptions,
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
            HierarchyOptions = hierarchyOptions,
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "my-hierarchy/MySubscriptionName" } },
        };

        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.Mode = TopicRoutingMode.CorrelationFilter);

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
            HierarchyOptions = hierarchyOptions,
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "my-hierarchy/MySubscriptionName" } },
        };

        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.Mode = TopicRoutingMode.CorrelationFilter);

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
    public async Task Should_unsubscribe_sql_filter_subscription_mapping()
    {
        var topologyOptions = new TopologyOptions();
        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.Mode = TopicRoutingMode.SqlLikeFilter);

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteRule(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue'"));
    }

    [Test]
    public async Task Should_unsubscribe_unmapped_event_using_sql_filter_fallback_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.SqlLikeFilter
            }
        };

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteRule(topicName: 'SharedTopic', subscriptionName: 'SubscribingQueue'"));
    }

    [Test]
    public async Task Should_unsubscribe_unmapped_event_using_correlation_filter_fallback_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            }
        };

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteRule(topicName: 'SharedTopic', subscriptionName: 'SubscribingQueue'"));
    }

    [Test]
    public async Task Should_unsubscribe_using_hashed_rule_name_for_long_event_type_names()
    {
        var topologyOptions = new TopologyOptions();
        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<VeryLongEventTypeNameThatShouldGenerateAHashedRuleNameForDeletionPath>("MyTopic", options => options.Mode = TopicRoutingMode.CorrelationFilter);

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(VeryLongEventTypeNameThatShouldGenerateAHashedRuleNameForDeletionPath)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("ruleName: 'Rule-"));
    }

    [Test]
    public async Task Should_delete_subscription_when_unsubscribing_not_multiplexed_mapping()
    {
        var topologyOptions = new TopologyOptions();
        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic", options => options.Mode = TopicRoutingMode.NotMultiplexed);

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteSubscription(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue')"));
    }

    [Test]
    public async Task Should_delete_subscription_when_unsubscribing_default_mapping()
    {
        var topologyOptions = new TopologyOptions();
        var topology = (TopicPerEventTopology)TopicTopology.FromOptions(topologyOptions);
        topology.SubscribeTo<MyEvent1>("MyTopic");

        var builder = new StringBuilder();
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(builder),
            AdministrationClient = new RecordingServiceBusAdministrationClient(builder)
        }, topologyOptions, new StartupDiagnosticEntries());

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(builder.ToString(), Does.Contain("DeleteSubscription(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue')"));
    }

    [Test]
    public async Task Should_apply_subscription_name_override_for_non_namespaced_queue_when_hierarchy_enabled()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyOptions = hierarchyOptions,
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("MyTopic", TopicRoutingMode.CorrelationFilter)] }
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
    public async Task Should_use_fallback_topic_routing_mode_for_unconfigured_subscription()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            }
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

        Assert.That(builder.ToString(), Does.Contain("CreateRuleOptions(topicName: 'SharedTopic', subscriptionName: 'SubscribingQueue')"));
    }

    [Test]
    public async Task Should_use_sql_filter_fallback_topic_routing_mode_for_unconfigured_subscription()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.SqlLikeFilter
            }
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

        Assert.That(builder.ToString(), Does.Contain("CreateRuleOptions(topicName: 'SharedTopic', subscriptionName: 'SubscribingQueue')"));
        Assert.That(builder.ToString(), Does.Contain("\"filter-type\": \"sql\""));
    }

    [Test]
    public async Task Should_not_apply_fallback_topic_routing_mode_to_mapped_subscription_with_default_routing_mode()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            },
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

        Assert.That(builder.ToString(), Does.Contain("\"TopicName\": \"MyTopic\""));
        Assert.That(builder.ToString(), Does.Not.Contain("CreateRuleOptions(topicName: 'MyTopic', subscriptionName: 'SubscribingQueue')"));
    }

    [Test]
    public void Should_allow_mixing_not_multiplexed_and_default_subscription_entries_for_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.NotMultiplexed)] },
                { typeof(MyEvent2).FullName, ["SharedTopic"] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
        };

        var client = new RecordingServiceBusClient(new StringBuilder());
        var administrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder());

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
    }

    [Test]
    public void Should_allow_mixing_default_and_not_multiplexed_subscription_entries_for_same_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["SharedTopic"] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.NotMultiplexed)] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
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
            HierarchyOptions = hierarchyOptions,
            SubscribedEventToTopicsMap =
            {
                { typeof(IMyEvent).FullName, [new SubscriptionEntry(sharedTopicName, TopicRoutingMode.CorrelationFilter)] }
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

    [Test]
    public async Task Should_apply_subscription_name_override_for_namespaced_queue_when_hierarchy_enabled()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var topologyOptions = new TopologyOptions
        {
            HierarchyOptions = hierarchyOptions,
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["MyTopic"] }
            },
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "MySubscriptionName" } },
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

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_inherit_fallback_mode_for_mapped_entry_pointing_at_fallback_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent3).FullName, ["SharedTopic"] }
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent3))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public void Should_keep_explicit_subscription_mode_when_entry_topic_matches_fallback()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent3).FullName, [new SubscriptionEntry("SharedTopic", TopicRoutingMode.NotMultiplexed)] }
            }
        };

        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(new StringBuilder()),
            AdministrationClient = new RecordingServiceBusAdministrationClient(new StringBuilder())
        }, topologyOptions, new StartupDiagnosticEntries());

        Assert.DoesNotThrowAsync(async () =>
            await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent3))], new ContextBag()));
    }

    [Test]
    public async Task Should_inherit_fallback_mode_for_mapped_entry_pointing_at_fallback_topic_under_hierarchy()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var destinationManager = new DestinationManager(hierarchyOptions);
        var queueName = destinationManager.GetDestination("SubscribingQueue");
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions
            {
                TopicName = "SharedTopic",
                RoutingMode = TopicRoutingMode.CorrelationFilter
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent3).FullName, ["SharedTopic"] }
            },
            QueueNameToSubscriptionNameMap = { { queueName, destinationManager.GetDestination("MySubscriptionName") } },
            HierarchyOptions = hierarchyOptions,
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

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent3))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_strip_hierarchy_prefix_from_hierarchical_subscription_name_override()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        var destinationManager = new DestinationManager(hierarchyOptions);
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [destinationManager.GetDestination("MyTopic1")] }
            },
            QueueNameToSubscriptionNameMap = { { "my-hierarchy/SubscribingQueue", "my-hierarchy/MySubscriptionName" } },
            HierarchyOptions = hierarchyOptions,
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

        Approver.Verify(builder.ToString());
    }

    class MyEvent1;
    class MyEvent2;
    class VeryLongEventTypeNameThatShouldGenerateAHashedRuleNameForDeletionPath;
    interface IMyEvent;
    class MyEvent3;
}