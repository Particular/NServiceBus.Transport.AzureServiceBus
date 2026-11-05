namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Extensibility;
using Logging;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NServiceBus.Testing;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class TopicPerEventSubscriptionManagerForwardingTests
{
    [Test]
    public async Task Should_start_forwarders_for_mapped_events_and_fallback_topic_when_not_subscribed()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1", "Topic2"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            },
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[]
        {
            "Topic1/SubscribingQueue",
            "Topic2/SubscribingQueue",
            "FallbackTopic/SubscribingQueue"
        }));
    }

    [Test]
    public async Task Should_apply_hierarchy_namespace_to_forwarded_topics()
    {
        var topologyOptions = new TopologyOptions
        {
            HierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" },
            SubscribedEventToTopicsMap = { { typeof(MyEvent1).FullName, ["Topic1"] } },
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, subscribingQueueName: "my-hierarchy/SubscribingQueue");

        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[]
        {
            "my-hierarchy/Topic1/SubscribingQueue",
            "my-hierarchy/FallbackTopic/SubscribingQueue"
        }));
    }

    [Test]
    public async Task Should_also_forward_from_the_unprefixed_fallback_topic_when_types_are_excluded_from_the_hierarchy()
    {
        var hierarchyOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
        hierarchyOptions.ExcludeMessageType<MyEvent2>();
        var topologyOptions = new TopologyOptions
        {
            HierarchyOptions = hierarchyOptions,
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, subscribingQueueName: "my-hierarchy/SubscribingQueue");

        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[]
        {
            "my-hierarchy/FallbackTopic/SubscribingQueue",
            "FallbackTopic/SubscribingQueue"
        }));
    }

    [Test]
    public async Task Should_not_start_forwarders_without_mapped_events_or_fallback_topic()
    {
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(new TopologyOptions(), clientFactory);

        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.Processors, Is.Empty);
    }

    [Test]
    public async Task Should_not_start_forwarders_from_topology_mapping_when_auto_subscribe_is_active()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap = { { typeof(MyEvent1).FullName, ["Topic1"] } },
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, autoSubscribeActive: true);

        await subscriptionManager.StartForwarders();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.Processors, Is.Empty);
            Assert.That(logOutput.ToString(), Is.Empty);
        }
    }

    [Test]
    public async Task Should_not_log_missing_mapping_when_auto_subscribe_is_active()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var subscriptionManager = CreateSubscriptionManager(new TopologyOptions(), new FakeForwardingClientFactory(), autoSubscribeActive: true);

        await subscriptionManager.StartForwarders();

        Assert.That(logOutput.ToString(), Is.Empty);
    }

    [Test]
    public async Task Should_log_missing_mapping_when_auto_subscribe_is_disabled()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Info);
#pragma warning restore CS0618 // Type or member is obsolete

        var subscriptionManager = CreateSubscriptionManager(new TopologyOptions(), new FakeForwardingClientFactory());

        await subscriptionManager.StartForwarders();

        Assert.That(logOutput.ToString(), Does.Contain("Auto-subscribe is disabled and neither subscribed event mappings nor a fallback topic are configured"));
    }

    [Test]
    public async Task Should_start_forwarders_for_explicit_subscriptions_when_auto_subscribe_is_active()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, autoSubscribeActive: true);

        await subscriptionManager.StartForwarders();
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
    }

    [Test]
    public async Task Should_not_start_forwarders_when_sessions_are_disabled()
    {
        var topologyOptions = new TopologyOptions { SubscribedEventToTopicsMap = { { typeof(MyEvent1).FullName, ["Topic1"] } } };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, requiresSession: false);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());
        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.Processors, Is.Empty);
    }

    [Test]
    public async Task Should_only_start_forwarders_for_subscribed_events_once_started()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            },
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.Empty);

        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
    }

    [Test]
    public async Task Should_start_forwarders_for_all_subscribed_topics_when_provisioning_fails_for_one()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["FailingTopic", "Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] },
                { typeof(MyEvent3).FullName, ["Topic3"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, new FailingAdministrationClient("FailingTopic"));

        Assert.ThrowsAsync<ServiceBusException>(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[]
        {
            "FailingTopic/SubscribingQueue",
            "Topic1/SubscribingQueue",
            "Topic2/SubscribingQueue"
        }));
    }

    [Test]
    public async Task Should_keep_provisioning_exception_and_stop_subscribing_when_a_forwarder_fails_to_start()
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Error);
#pragma warning restore CS0618 // Type or member is obsolete

        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["FailingTopic", "Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, new FailingAdministrationClient("FailingTopic"), autoSubscribeActive: true);
        await subscriptionManager.StartForwarders();

        var starts = 0;
        clientFactory.StartProcessing = () => ++starts == 2 ? throw new InvalidOperationException("start failed") : Task.CompletedTask;

        Assert.ThrowsAsync<ServiceBusException>(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "FailingTopic/SubscribingQueue" }));
            Assert.That(clientFactory.Processors.Select(processor => processor.EntityPath), Does.Not.Contain("Topic2/SubscribingQueue"));
            Assert.That(logOutput.ToString(), Does.Contain($"Failed to start forwarding '{typeof(MyEvent1).FullName}' from subscription 'SubscribingQueue' on topic 'Topic1'"));
        }
    }

    [Test]
    public async Task Should_throw_and_stop_subscribing_when_a_forwarder_fails_to_start_after_provisioning()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic2"] },
                { typeof(MyEvent3).FullName, ["Topic3"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory, autoSubscribeActive: true);
        await subscriptionManager.StartForwarders();

        var starts = 0;
        clientFactory.StartProcessing = () => ++starts == 2 ? throw new InvalidOperationException("start failed") : Task.CompletedTask;

        Assert.ThrowsAsync<InvalidOperationException>(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2)), new MessageMetadata(typeof(MyEvent3))], new ContextBag()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
            Assert.That(clientFactory.Processors.Select(processor => processor.EntityPath), Does.Not.Contain("Topic3/SubscribingQueue"));
        }
    }

    [Test]
    public async Task Should_start_forwarder_immediately_when_subscribing_after_started()
    {
        var topologyOptions = new TopologyOptions { SubscribedEventToTopicsMap = { { typeof(MyEvent1).FullName, ["Topic1"] } } };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.StartForwarders();
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue", typeof(MyEvent2).FullName + "/SubscribingQueue" }));
    }

    [Test]
    public async Task Should_not_start_duplicate_forwarders_when_subscribing_after_started()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["Topic1"] },
                { typeof(MyEvent2).FullName, ["Topic1", "Topic2"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.StartForwarders();
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Assert.That(clientFactory.Processors, Has.Exactly(2).Items);
        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue", "Topic2/SubscribingQueue" }));
    }

    [Test]
    public async Task Should_not_start_forwarders_unsubscribed_before_started()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["SharedTopic"] },
                { typeof(MyEvent2).FullName, ["SharedTopic", "Topic2"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());
        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent2)), new ContextBag());
        await subscriptionManager.StartForwarders();

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "SharedTopic/SubscribingQueue" }));
    }

    [Test]
    public async Task Should_keep_forwarder_when_unsubscribing_one_of_several_events_on_a_shared_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, ["SharedTopic"] },
                { typeof(MyEvent2).FullName, ["SharedTopic"] }
            }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.StartForwarders();
        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "SharedTopic/SubscribingQueue" }));

        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent2)), new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.Empty);
    }

    [Test]
    public async Task Should_keep_fallback_forwarder_when_unsubscribing_an_event_routed_through_the_fallback_topic()
    {
        var topologyOptions = new TopologyOptions
        {
            FallbackTopic = new FallbackTopicOptions { TopicName = "FallbackTopic", RoutingMode = TopicRoutingMode.CorrelationFilter }
        };
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(topologyOptions, clientFactory);

        await subscriptionManager.StartForwarders();
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());
        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "FallbackTopic/SubscribingQueue" }));
    }

    internal static TopicPerEventTopologySubscriptionManager CreateSubscriptionManager(TopologyOptions topologyOptions,
        FakeForwardingClientFactory clientFactory,
        ServiceBusAdministrationClient administrationClient = null,
        string subscribingQueueName = "SubscribingQueue",
        bool requiresSession = true,
        bool autoSubscribeActive = false) =>
        new(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = subscribingQueueName,
            Client = new RecordingServiceBusClient(),
            AdministrationClient = administrationClient ?? new RecordingServiceBusAdministrationClient(),
            RequiresSession = requiresSession,
            AutoSubscribeActive = autoSubscribeActive,
            ForwarderClientFactory = clientFactory.Create,
            CriticalErrorAction = (_, _, _) => { }
        }, topologyOptions, new StartupDiagnosticEntries());

    internal class FailingAdministrationClient(string failingTopic = null) : RecordingServiceBusAdministrationClient
    {
        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions rule, CancellationToken cancellationToken = default) =>
            failingTopic == null || string.Equals(options.TopicName, failingTopic, StringComparison.Ordinal)
                ? throw new ServiceBusException("provisioning failed", ServiceBusFailureReason.GeneralError)
                : base.CreateSubscriptionAsync(options, rule, cancellationToken);
    }

    class MyEvent1;

    class MyEvent2;

    class MyEvent3;
}