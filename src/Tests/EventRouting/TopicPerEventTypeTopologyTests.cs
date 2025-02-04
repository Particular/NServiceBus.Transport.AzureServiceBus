namespace NServiceBus.Transport.AzureServiceBus.Tests;

using NUnit.Framework;

[TestFixture]
public class TopicPerEventTypeTopologyTests
{
    [Test]
    public void PublishDestination_Should_return_mapped_topic_when_event_is_mapped()
    {
        var topologyOptions = new TopologyOptions
        {
            PublishedEventToTopicsMap =
            {
                [typeof(MyEvent).FullName] = "MyTopic"
            }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetPublishDestination(typeof(MyEvent));

        Assert.That(result, Is.EqualTo("MyTopic"));
    }

    [Test]
    public void SubscribeDestinations_Should_return_mapped_topics_when_event_is_mapped_for_subscription()
    {
        var topologyOptions = new TopologyOptions
        {
            SubscribedEventToTopicsMap =
            {
                [typeof(MyEvent).FullName] = ["Topic1", "Topic2"]
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue");

        Assert.That(result, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo("Topic1"));
            Assert.That(result[0].SubscriptionName, Is.EqualTo("MySubscriptionName"));
            Assert.That(result[0].Rule, Is.Null);
            Assert.That(result[1].Topic, Is.EqualTo("Topic2"));
            Assert.That(result[1].SubscriptionName, Is.EqualTo("MySubscriptionName"));
            Assert.That(result[1].Rule, Is.Null);
        });
    }

    [Test]
    public void SubscribeDestinations_with_options_Should_return_default_when_event_is_not_mapped()
    {
        var topologyOptions = new TopologyOptions();

        var topology = TopicTopology.FromOptions(topologyOptions);

        var result = topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue");

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Topic, Is.EqualTo(typeof(MyEvent).FullName));
            Assert.That(result[0].SubscriptionName, Is.EqualTo("SubscribingQueue"));
            Assert.That(result[0].Rule, Is.Null);
        });
    }

    class MyEvent;
}