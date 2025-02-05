namespace NServiceBus.Transport.AzureServiceBus.Tests;

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
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = "PublishTopic",
            TopicToSubscribeOn = "SubscribeTopic",
            EventsToMigrateMap = { typeof(MyEvent).FullName },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, "MyRuleName" } }
        };

        var client = new RecordingServiceBusClient();
        var administrationClient = new RecordingServiceBusAdministrationClient();

        var subscriptionManager = new MigrationTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = client,
            AdministrationClient = administrationClient
        }, topologyOptions);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new ContextBag());

        Approver.Verify(client.ToString());
    }

    class MyEvent;
}