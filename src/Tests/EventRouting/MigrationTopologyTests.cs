namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class MigrationTopologyTests
{
    [Test]
    public void Should_self_validate()
    {
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = new string('a', 261),
            TopicToSubscribeOn = new string('a', 261),
            PublishedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, new string('c', 261) },
                { typeof(MyEventMappedTwice).FullName, "MyEventMappedTwice" }
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] },
                { typeof(MyEventMappedTwice).FullName, ["MyEventMappedTwice"] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, new string('g', 51) } },
            EventsToMigrateMap = { typeof(MyEventMappedTwice).FullName },
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    class MyEvent;
    class MyEventMappedTwice;
}