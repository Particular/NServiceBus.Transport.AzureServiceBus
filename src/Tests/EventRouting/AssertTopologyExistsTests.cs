namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Text;
using System.Threading.Tasks;
using Extensibility;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class AssertTopologyExistsTests
{
    [Test]
    public async Task Should_forward_from_existing_subscriptions_without_using_the_administration_client()
    {
        var administration = new StringBuilder();
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(new RecordingServiceBusClient(), administration, clientFactory);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());
        subscriptionManager.ThrowIfSubscribingFailed();
        await subscriptionManager.StartForwarders();

        Assert.Multiple(() =>
        {
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue", "Topic2/SubscribingQueue" }));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task Should_fail_subscribing_and_starting_when_a_subscription_does_not_exist()
    {
        var client = new RecordingServiceBusClient();
        client.MissingSubscriptions.Add("Topic2/SubscribingQueue");
        var administration = new StringBuilder();
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(client, administration, clientFactory);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
        await subscriptionManager.StartForwarders();

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("'Topic2'").And.Not.Contain("'Topic1'"));
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.InvalidOperationException.With.Message.EqualTo(exception.Message));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task Should_add_filter_rules_to_existing_subscriptions()
    {
        var client = new RecordingServiceBusClient();
        var administration = new StringBuilder();
        var subscriptionManager = CreateSubscriptionManager(client, administration, new FakeForwardingClientFactory(), routingMode: TopicRoutingMode.CorrelationFilter);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.Multiple(() =>
        {
            Assert.That(client.ToString(), Does.Contain("CreateRuleOptions(topicName: 'Topic1', subscriptionName: 'SubscribingQueue')"));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [TestCase(TopicRoutingMode.NotMultiplexed)]
    [TestCase(TopicRoutingMode.CorrelationFilter)]
    public async Task Should_reject_unsubscribing_and_keep_forwarding(TopicRoutingMode routingMode)
    {
        var client = new RecordingServiceBusClient();
        var administration = new StringBuilder();
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(client, administration, clientFactory, routingMode: routingMode);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());
        await subscriptionManager.StartForwarders();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("cannot unsubscribe from").And.Contain(typeof(MyEvent1).FullName));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
            Assert.That(client.ToString(), Does.Not.Contain("DeleteRule"));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task Should_fail_starting_when_checking_a_subscription_fails_unexpectedly()
    {
        var client = new RecordingServiceBusClient();
        var probeFailure = new UnauthorizedAccessException("Listen claim required");
        client.ProbeFailures.Add("Topic2/SubscribingQueue", probeFailure);
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(client, new StringBuilder(), clientFactory);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
        await subscriptionManager.StartForwarders();

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("'Topic2'").And.Contain(probeFailure.Message).And.Not.Contain("'Topic1'"));
            Assert.That(exception.InnerException, Is.SameAs(probeFailure));
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.InvalidOperationException.With.Message.EqualTo(exception.Message));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
        });
    }

    [Test]
    public async Task Should_clear_only_the_failures_of_subscriptions_that_exist_when_subscribing_again()
    {
        var client = new RecordingServiceBusClient();
        client.MissingSubscriptions.Add("Topic1/SubscribingQueue");
        client.MissingSubscriptions.Add("Topic2/SubscribingQueue");
        var subscriptionManager = CreateSubscriptionManager(client, new StringBuilder(), new FakeForwardingClientFactory());

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));

        client.MissingSubscriptions.Remove("Topic1/SubscribingQueue");
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(),
            Throws.InvalidOperationException.With.Message.Contain("'Topic2'").And.Message.Not.Contain("'Topic1'"));

        client.MissingSubscriptions.Remove("Topic2/SubscribingQueue");
        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.Nothing);
    }

    [Test]
    public async Task Should_check_subscriptions_and_add_rules_without_using_the_administration_client_in_the_migration_topology()
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = false };
        var administration = new StringBuilder();
        var subscriptionManager = CreateMigrationSubscriptionManager(client, administration);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Assert.Multiple(() =>
        {
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.Nothing);
            Assert.That(client.ToString(), Does.Contain("CreateRuleOptions(topicName: 'SubscribeTopic', subscriptionName: 'SubscribingQueue')"));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [TestCase("SubscribeTopic")]
    [TestCase("MigratedTopic")]
    public void Should_fail_subscribing_and_starting_when_a_subscription_does_not_exist_in_the_migration_topology(string missingTopic)
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = false };
        client.MissingSubscriptions.Add($"{missingTopic}/SubscribingQueue");
        var administration = new StringBuilder();
        var subscriptionManager = CreateMigrationSubscriptionManager(client, administration);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain($"must exist on topic '{missingTopic}'"));
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.InvalidOperationException.With.Message.EqualTo(exception.Message));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [Test]
    public void Should_reject_unsubscribing_in_the_migration_topology()
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = false };
        var administration = new StringBuilder();
        var subscriptionManager = CreateMigrationSubscriptionManager(client, administration);

        Assert.Multiple(() =>
        {
            Assert.That(() => subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag()),
                Throws.InvalidOperationException.With.Message.Contain("cannot unsubscribe from"));
            Assert.That(() => subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent2)), new ContextBag()),
                Throws.InvalidOperationException.With.Message.Contain("cannot unsubscribe from"));
            Assert.That(client.ToString(), Does.Not.Contain("DeleteRule"));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    static MigrationTopologySubscriptionManager CreateMigrationSubscriptionManager(RecordingServiceBusClient client, StringBuilder administration) =>
        new(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = new RecordingServiceBusAdministrationClient(administration),
            AssertTopologyExists = true,
            SetupInfrastructure = true,
            CriticalErrorAction = (_, _, _) => { }
        },
#pragma warning disable CS0618 // Type or member is obsolete
        new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = "PublishTopic",
            TopicToSubscribeOn = "SubscribeTopic",
            EventsToMigrateMap = { typeof(MyEvent1).FullName },
            SubscribedEventToTopicsMap = { { typeof(MyEvent2).FullName, ["MigratedTopic"] } }
        }, new StartupDiagnosticEntries());

    [TestCase(true, false, "does not require sessions, but the endpoint has sessions enabled")]
    [TestCase(false, true, "requires sessions, but the endpoint does not have sessions enabled")]
    public void Should_reject_a_subscription_whose_session_setting_does_not_match(bool endpointRequiresSession, bool subscriptionRequiresSession, string expectedMessage)
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = subscriptionRequiresSession };
        var subscriptionManager = CreateSubscriptionManager(client, new StringBuilder(), new FakeForwardingClientFactory(), requiresSession: endpointRequiresSession);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(expectedMessage));
            // Core's auto-subscribe logs the exception and continues, so the receivers must fail to start
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.InvalidOperationException.With.Message.EqualTo(exception.Message));
        });
    }

    [Test]
    public async Task Should_forward_from_matching_subscriptions_when_another_subscription_has_a_different_session_setting()
    {
        var client = new RecordingServiceBusClient();
        client.SubscriptionsWithoutSessions.Add("Topic2/SubscribingQueue");
        var administration = new StringBuilder();
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(client, administration, clientFactory);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag()));
        await subscriptionManager.StartForwarders();

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("'Topic2'").And.Not.Contain("'Topic1'"));
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.InvalidOperationException.With.Message.EqualTo(exception.Message));
            Assert.That(clientFactory.RunningForwarders, Is.EquivalentTo(new[] { "Topic1/SubscribingQueue" }));
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task Should_accept_existing_subscriptions_without_sessions_on_endpoints_without_sessions()
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = false };
        var administration = new StringBuilder();
        var subscriptionManager = CreateSubscriptionManager(client, administration, new FakeForwardingClientFactory(), requiresSession: false);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());

        Assert.Multiple(() =>
        {
            Assert.That(() => subscriptionManager.ThrowIfSubscribingFailed(), Throws.Nothing);
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    static TopicPerEventTopologySubscriptionManager CreateSubscriptionManager(RecordingServiceBusClient client, StringBuilder administration,
        FakeForwardingClientFactory clientFactory, TopicRoutingMode routingMode = TopicRoutingMode.NotMultiplexed, bool requiresSession = true) =>
        new(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = new RecordingServiceBusAdministrationClient(administration),
            RequiresSession = requiresSession,
            AssertTopologyExists = true,
            SetupInfrastructure = true,
            ForwarderClientFactory = clientFactory.Create,
            CriticalErrorAction = (_, _, _) => { }
        }, new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent1).FullName, [new SubscriptionEntry("Topic1", routingMode)] },
                { typeof(MyEvent2).FullName, [new SubscriptionEntry("Topic2", routingMode)] }
            }
        }, new StartupDiagnosticEntries());

    class MyEvent1;

    class MyEvent2;
}
