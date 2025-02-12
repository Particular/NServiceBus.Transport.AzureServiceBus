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
            },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] },
                { typeof(MyEventMappedTwice).FullName, ["MyEventMappedTwice"] }
            },
            QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
            SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, new string('g', 51) } },
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        var validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    [Test]
    public void Should_self_validate_consistency()
    {
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = "TopicToPublishTo",
            TopicToSubscribeOn = "TopicToSubscribeOn",
            PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, "TopicToPublishTo" } },
            SubscribedEventToTopicsMap =
            {
                { typeof(MyEvent).FullName, ["SomeOtherTopic", "TopicToSubscribeOn"] },
                { typeof(MyEventMappedTwice).FullName, ["MyEventMappedTwice"] }
            },
            EventsToMigrateMap = { typeof(MyEventMappedTwice).FullName }
        };

        var topology = TopicTopology.FromOptions(topologyOptions);

        ValidationException validationException = Assert.Catch<ValidationException>(() => topology.Validate());

        Approver.Verify(validationException.Message);
    }

    // With the generic host validation can already be done at startup and this allows disabling further validation
    // for advanced scenarios to save startup time.
    [Test]
    public void Should_allow_disabling_validation()
    {
        var topologyOptions = new MigrationTopologyOptions
        {
            TopicToPublishTo = new string('a', 261),
            TopicToSubscribeOn = new string('a', 261)
        };

        var topology = TopicTopology.FromOptions(topologyOptions);
        topology.OptionsValidator = new TopologyOptionsDisableValidationValidator();

        Assert.DoesNotThrow(() => topology.Validate());
    }

    class MyEvent;
    class MyEventMappedTwice;
}