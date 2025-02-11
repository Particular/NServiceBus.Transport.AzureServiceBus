namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class TopicPerEventTopologyTests
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
    public void Should_self_validate()
    {
        var topologyOptions = new TopologyOptions
        {
            PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, new string('c', 261) } },
            SubscribedEventToTopicsMap = { { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] } },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    class MyEvent;
}