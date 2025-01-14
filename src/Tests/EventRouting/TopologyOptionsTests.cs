#nullable enable

namespace NServiceBus.Transport.AzureServiceBus.Tests;

using NUnit.Framework;

[TestFixture]
public class TopologyOptionsTests
{
    TopologyOptions topologyOptions;

    [SetUp]
    public void SetUp() => topologyOptions = new TopologyOptions();

    [Test]
    public void PublishDestination_Should_return_mapped_topic_when_event_is_mapped()
    {
        topologyOptions.PublishedEventToTopicsMap["NServiceBus.Transport.AzureServiceBus.Tests.MyEvent"] = "MyTopic";

        var result = topologyOptions.GetPublishDestination(typeof(MyEvent));

        Assert.That(result, Is.EqualTo("MyTopic"));
    }

    [Test]
    public void SubscribeDestinations_Should_return_mapped_topics_when_event_is_mapped_for_subscription()
    {
        topologyOptions.SubscribedEventToTopicsMap["NServiceBus.Transport.AzureServiceBus.Tests.MyEvent"] = ["Topic1", "Topic2"];

        var result = topologyOptions.GetSubscribeDestinations(typeof(MyEvent));

        Assert.That(result, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo("Topic1"));
            Assert.That(result[0].RequiresRule, Is.False);
            Assert.That(result[1].Topic, Is.EqualTo("Topic2"));
            Assert.That(result[1].RequiresRule, Is.False);
        });
    }

    [Test]
    public void SubscribeDestinations_Should_return_migration_topic_when_event_is_mapped_for_migration()
    {
        topologyOptions.EventsToTopicMigrationMap["NServiceBus.Transport.AzureServiceBus.Tests.MyEvent"] = ("PublishTopic", "SubscribeTopic");

        var result = topologyOptions.GetSubscribeDestinations(typeof(MyEvent));

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo("SubscribeTopic"));
            Assert.That(result[0].RequiresRule, Is.True);
        });
    }

    [Test]
    public void SubscribeDestinations_Should_return_default_when_event_is_not_mapped()
    {
        var result = topologyOptions.GetSubscribeDestinations(typeof(MyEvent));

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo("NServiceBus.Transport.AzureServiceBus.Tests.MyEvent"));
            Assert.That(result[0].RequiresRule, Is.False);
        });
    }
}

public class MyEvent;