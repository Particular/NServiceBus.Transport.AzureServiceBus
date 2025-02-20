namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text.Json;
using NUnit.Framework;

[TestFixture]
public class SubscribeEventToTopicsMapConverterTests
{
    [Test]
    public void Defaults_to_non_migration_when_no_type_specified()
    {
        const string jsonPayload = """
                                   {
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : "SomeTopic"
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo(["SomeTopic"]));
        });
    }

    [Test]
    public void Supports_single_element()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : "SomeTopic"
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo(["SomeTopic"]));
        });
    }

    [Test]
    public void Supports_multiple_elements()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : ["SomeTopic", "AnotherTopic"]
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo(["SomeTopic", "AnotherTopic"]));
        });
    }

    [Test]
    public void Supports_mixing_elements()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent1" : ["SomeTopic", "AnotherTopic"],
                                       "MyEvent2" : "SomeTopic"
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(2));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent1"],
                Is.EquivalentTo(["SomeTopic", "AnotherTopic"]));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent2"],
                Is.EquivalentTo(["SomeTopic"]));
        });
    }
}