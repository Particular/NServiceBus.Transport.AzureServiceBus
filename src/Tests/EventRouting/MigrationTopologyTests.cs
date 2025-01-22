namespace NServiceBus.Transport.AzureServiceBus.Tests;

using NUnit.Framework;

[TestFixture]
public class MigrationTopologyTests
{
    [Test]
    public void SubscribeDestinations_Should_return_migration_topic_when_event_is_mapped_for_migration()
    {
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = "PublishTopic",
            TopicToSubscribeOn = "SubscribeTopic",
            EventsToMigrateMap = { typeof(MyEvent).FullName },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, "MyRuleName" } }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue");

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo("SubscribeTopic"));
            Assert.That(result[0].SubscriptionName, Is.EqualTo("MySubscriptionName"));
            Assert.That(result[0].RuleInfo, Is.Not.Null);
            Assert.That(result[0].RuleInfo.Value.RuleName, Is.EqualTo("MyRuleName"));
            Assert.That(result[0].RuleInfo.Value.RuleFilter, Is.EqualTo("[NServiceBus.EnclosedMessageTypes] LIKE '%NServiceBus.Transport.AzureServiceBus.Tests.MigrationTopologyTests+MyEvent%'"));
        });
    }

    [Test]
    public void SubscribeDestinations_with_migration_options_Should_throw_when_event_is_not_mapped()
    {
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = "TopicToPublishTo",
            TopicToSubscribeOn = "TopicToSubscribeOn",
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        Assert.That(() => topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue"), Throws.Exception);
    }

    class MyEvent;
}