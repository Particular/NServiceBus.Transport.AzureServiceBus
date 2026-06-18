namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text.Json;
using NUnit.Framework;

[TestFixture]
public class SubscribeEventToTopicsMapConverterTests
{
    [Test]
    public void Initializes_empty_collections_when_data_is_missing()
    {
        const string jsonPayload = """
                                   {
                                      "$type" : "topology-options"
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Is.Not.Null);
            Assert.That(deserialized.SubscribedEventToTopicsMap, Is.Not.Null);
            Assert.That(deserialized.QueueNameToSubscriptionNameMap, Is.Not.Null);
        }
    }

    [Test]
    public void Initializes_empty_collections_when_data_is_missing_migration_options()
    {
        const string jsonPayload = """
                                   {
                                      "$type" : "migration-topology-options",
                                      "TopicToPublishTo" : "bundle-1",
                                      "TopicToSubscribeOn" : "bundle-1"
                                   }
                                   """;

#pragma warning disable CS0618 // Type or member is obsolete
        MigrationTopologyOptions deserialized = JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.MigrationTopologyOptions);
#pragma warning restore CS0618 // Type or member is obsolete

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Is.Not.Null);
            Assert.That(deserialized.SubscribedEventToTopicsMap, Is.Not.Null);
            Assert.That(deserialized.QueueNameToSubscriptionNameMap, Is.Not.Null);
            Assert.That(deserialized.EventsToMigrateMap, Is.Not.Null);
            Assert.That(deserialized.SubscribedEventToRuleNameMap, Is.Not.Null);
        }
    }

    [Test]
    public void Can_provide_migration_topology_options_with_type_annotations()
    {
        const string jsonPayload = """
                                   {
                                      "$type" : "migration-topology-options",
                                      "TopicToPublishTo" : "bundle-1",
                                      "TopicToSubscribeOn" : "bundle-1"
                                   }
                                   """;

#pragma warning disable CS0618 // Type or member is obsolete
        var deserialized = (MigrationTopologyOptions)JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);
#pragma warning restore CS0618 // Type or member is obsolete

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.TopicToSubscribeOn, Is.EqualTo("bundle-1"));
            Assert.That(deserialized.TopicToPublishTo, Is.EqualTo("bundle-1"));
        }
    }

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic")]));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic")]));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic"),
                    new SubscriptionEntry("AnotherTopic")]));
        }
    }

    [Test]
    public void Supports_object_form_as_single_value()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : {"Topic":"SomeTopic","RoutingMode":"CorrelationFilter"}
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic", TopicRoutingMode.CorrelationFilter)]));
        }
    }

    [Test]
    public void Supports_object_form_in_array()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : [
                                         {"Topic":"SomeTopic","RoutingMode":"CorrelationFilter"},
                                         {"Topic":"AnotherTopic","RoutingMode":"NotMultiplexed"}
                                       ]
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic", TopicRoutingMode.CorrelationFilter),
                    new SubscriptionEntry("AnotherTopic", TopicRoutingMode.NotMultiplexed)]));
        }
    }

    [Test]
    public void Supports_mixed_string_and_object_in_array()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "SubscribedEventToTopicsMap" : {
                                       "MyEvent" : [
                                         "SomeTopic",
                                         {"Topic":"AnotherTopic","RoutingMode":"SqlLikeFilter"}
                                       ]
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic"),
                    new SubscriptionEntry("AnotherTopic", TopicRoutingMode.SqlLikeFilter)]));
        }
    }

    [Test]
    public void Single_string_round_trips()
    {
        var options = new TopologyOptions
        {
            SubscribedEventToTopicsMap = new()
            {
                { "MyEvent", [new SubscriptionEntry("SomeTopic")] }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic")]));
        }
    }

    [Test]
    public void Array_of_strings_round_trips()
    {
        var options = new TopologyOptions
        {
            SubscribedEventToTopicsMap = new()
            {
                { "MyEvent", [new SubscriptionEntry("SomeTopic"), new SubscriptionEntry("AnotherTopic")] }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic"),
                    new SubscriptionEntry("AnotherTopic")]));
        }
    }

    [Test]
    public void Object_form_round_trips()
    {
        var options = new TopologyOptions
        {
            SubscribedEventToTopicsMap = new()
            {
                { "MyEvent", [new SubscriptionEntry("SomeTopic", TopicRoutingMode.CorrelationFilter)] }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic", TopicRoutingMode.CorrelationFilter)]));
        }
    }

    [Test]
    public void Mixed_single_and_array_round_trips()
    {
        var options = new TopologyOptions
        {
            SubscribedEventToTopicsMap = new()
            {
                { "MyEvent1", [new SubscriptionEntry("SomeTopic"), new SubscriptionEntry("AnotherTopic")] },
                { "MyEvent2", [new SubscriptionEntry("SingleTopic")] }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(2));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent1"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic"),
                    new SubscriptionEntry("AnotherTopic")]));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent2"],
                Is.EquivalentTo([new SubscriptionEntry("SingleTopic")]));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.SubscribedEventToTopicsMap, Has.Count.EqualTo(2));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent1"],
                Is.EquivalentTo([
                    new SubscriptionEntry("SomeTopic"),
                    new SubscriptionEntry("AnotherTopic")]));
            Assert.That(deserialized.SubscribedEventToTopicsMap["MyEvent2"],
                Is.EquivalentTo([new SubscriptionEntry("SomeTopic")]));
        }
    }
}