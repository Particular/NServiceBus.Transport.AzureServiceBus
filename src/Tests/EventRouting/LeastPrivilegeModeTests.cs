namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Text;
using System.Threading.Tasks;
using Extensibility;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class LeastPrivilegeModeTests
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

    [Test]
    public async Task Should_not_delete_subscriptions_when_unsubscribing()
    {
        var administration = new StringBuilder();
        var clientFactory = new FakeForwardingClientFactory();
        var subscriptionManager = CreateSubscriptionManager(new RecordingServiceBusClient(), administration, clientFactory);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag());
        await subscriptionManager.StartForwarders();
        await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent1)), new ContextBag());

        Assert.Multiple(() =>
        {
            Assert.That(clientFactory.RunningForwarders, Is.Empty);
            Assert.That(administration.ToString(), Is.Empty);
        });
    }

    [TestCase(true, false, "does not require sessions, but the endpoint has sessions enabled")]
    [TestCase(false, true, "requires sessions, but the endpoint does not have sessions enabled")]
    public void Should_reject_a_subscription_whose_session_setting_does_not_match(bool endpointRequiresSession, bool subscriptionRequiresSession, string expectedMessage)
    {
        var client = new RecordingServiceBusClient { SubscriptionsRequireSession = subscriptionRequiresSession };
        var subscriptionManager = CreateSubscriptionManager(client, new StringBuilder(), new FakeForwardingClientFactory(), requiresSession: endpointRequiresSession);

        Assert.That(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag()),
            Throws.InvalidOperationException.With.Message.Contains(expectedMessage));
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
            LeastPrivilegeMode = true,
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
