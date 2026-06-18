namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System.Text.Json;
using NUnit.Framework;

[TestFixture]
public class PublishedEventToTopicsMapConverterTests
{
    [Test]
    public void String_deserializes_to_null_routing_mode()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "PublishedEventToTopicsMap" : {
                                       "MyEvent" : "SomeTopic"
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic")));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"].RoutingMode, Is.Null);
        }
    }

    [Test]
    public void Object_form_deserializes_correlation_filter()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "PublishedEventToTopicsMap" : {
                                       "MyEvent" : {"Topic":"SomeTopic","RoutingMode":"CorrelationFilter"}
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic", TopicRoutingMode.CorrelationFilter)));
        }
    }

    [Test]
    public void Object_form_deserializes_not_multiplexed()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "PublishedEventToTopicsMap" : {
                                       "MyEvent" : {"Topic":"SomeTopic","RoutingMode":"NotMultiplexed"}
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic", TopicRoutingMode.NotMultiplexed)));
        }
    }

    [Test]
    public void Object_form_deserializes_sql_filter()
    {
        const string jsonPayload = """
                                   {
                                     "$type" : "topology-options",
                                     "PublishedEventToTopicsMap" : {
                                       "MyEvent" : {"Topic":"SomeTopic","RoutingMode":"SqlLikeFilter"}
                                     }
                                   }
                                   """;

        TopologyOptions deserialized =
            JsonSerializer.Deserialize(jsonPayload, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic", TopicRoutingMode.SqlLikeFilter)));
        }
    }

    [Test]
    public void Null_routing_mode_serializes_to_string()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic") }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.That(json, Does.Contain("\"MyEvent\": \"SomeTopic\""));
    }

    [Test]
    public void Correlation_filter_serializes_to_object()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic", TopicRoutingMode.CorrelationFilter) }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.That(json, Does.Contain("\"MyEvent\": {"));
        Assert.That(json, Does.Contain("\"Topic\": \"SomeTopic\""));
        Assert.That(json, Does.Contain("\"RoutingMode\": \"CorrelationFilter\""));
    }

    [Test]
    public void Not_multiplexed_serializes_to_object()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic", TopicRoutingMode.NotMultiplexed) }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.That(json, Does.Contain("\"MyEvent\": {"));
        Assert.That(json, Does.Contain("\"Topic\": \"SomeTopic\""));
        Assert.That(json, Does.Contain("\"RoutingMode\": \"NotMultiplexed\""));
    }

    [Test]
    public void Sql_filter_serializes_to_object()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic", TopicRoutingMode.SqlLikeFilter) }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);

        Assert.That(json, Does.Contain("\"MyEvent\": {"));
        Assert.That(json, Does.Contain("\"Topic\": \"SomeTopic\""));
        Assert.That(json, Does.Contain("\"RoutingMode\": \"SqlLikeFilter\""));
    }

    [Test]
    public void String_form_round_trips()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic") }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic")));
        }
    }

    [Test]
    public void Object_form_round_trips()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent", new PublishEntry("SomeTopic", TopicRoutingMode.CorrelationFilter) }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent"], Is.EqualTo(new PublishEntry("SomeTopic", TopicRoutingMode.CorrelationFilter)));
        }
    }

    [Test]
    public void Multiple_events_round_trips()
    {
        var options = new TopologyOptions
        {
            PublishedEventToTopicsMap = new()
            {
                { "MyEvent1", new PublishEntry("TopicA") },
                { "MyEvent2", new PublishEntry("TopicB", TopicRoutingMode.NotMultiplexed) }
            }
        };

        string json = JsonSerializer.Serialize(options, TopologyOptionsSerializationContext.Default.TopologyOptions);
        TopologyOptions deserialized = JsonSerializer.Deserialize(json, TopologyOptionsSerializationContext.Default.TopologyOptions);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deserialized.PublishedEventToTopicsMap, Has.Count.EqualTo(2));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent1"], Is.EqualTo(new PublishEntry("TopicA")));
            Assert.That(deserialized.PublishedEventToTopicsMap["MyEvent2"], Is.EqualTo(new PublishEntry("TopicB", TopicRoutingMode.NotMultiplexed)));
        }
    }
}
