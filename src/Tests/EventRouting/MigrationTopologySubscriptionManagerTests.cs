namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Threading.Tasks;
using Extensibility;
using NUnit.Framework;
using Particular.Approvals;
using Unicast.Messages;

[TestFixture]
public class MigrationTopologySubscriptionManagerTests
{
    [Test]
    public async Task Should_create_topology_for_events_to_migrate()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = "PublishTopic",
            TopicToSubscribeOn = "SubscribeTopic",
            EventsToMigrateMap =
            {
                typeof(MyEvent1).FullName,
                typeof(MyEvent2).FullName
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
            SubscribedEventToRuleNameMap =
            {
                { typeof(MyEvent1).FullName, "MyRuleName1" },
                { typeof(MyEvent2).FullName, "MyRuleName2" }
            }
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new MigrationTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_create_topology_for_migrated_and_not_migrated_events()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = "PublishTopic",
            TopicToSubscribeOn = "SubscribeTopic",
            EventsToMigrateMap = { typeof(MyEvent1).FullName },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent1).FullName, "MyRuleName" } },
            SubscribedEventToTopicsMap = { { typeof(MyEvent2).FullName, ["MyTopic1", "MyTopic2"] } }
        };

        var builder = new StringBuilder();
        var client = new RecordingServiceBusClient(builder);
        var administrationClient = new RecordingServiceBusAdministrationClient(builder);

        var subscriptionManager = new MigrationTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))], new ContextBag());

        Approver.Verify(builder.ToString());
    }

    [Test]
    public async Task Should_throw_when_event_is_not_mapped()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topologyOptions = new MigrationTopologyOptions
#pragma warning restore CS0618 // Type or member is obsolete
        {
            TopicToPublishTo = "TopicToPublishTo",
            TopicToSubscribeOn = "TopicToSubscribeOn",
        };

        var client = new RecordingServiceBusClient();
        var administrationClient = new RecordingServiceBusAdministrationClient();

        var subscriptionManager = new MigrationTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions);

        await Assert.ThatAsync(() => subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent1))], new ContextBag()), Throws.Exception);
    }

    class MyEvent1;
    class MyEvent2;
}