namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System.Text.Json;
using NUnit.Framework;

[TestFixture]
public class PublishEntryJsonConverterTests
{
    [Test]
    public void String_deserializes_to_null_routing_mode()
    {
        const string json = "\"MyTopic\"";

        PublishEntry entry = JsonSerializer.Deserialize<PublishEntry>(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.Null);
        }
    }

    [Test]
    public void Null_routing_mode_serializes_to_string()
    {
        var entry = new PublishEntry("MyTopic");

        string json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("\"MyTopic\""));
    }

    [Test]
    public void Topic_only_string_form_round_trips_without_changing_shape()
    {
        const string json = "\"MyTopic\"";

        PublishEntry entry = JsonSerializer.Deserialize<PublishEntry>(json);
        string serialized = JsonSerializer.Serialize(entry);

        Assert.That(serialized, Is.EqualTo(json));
    }

    [Test]
    public void Not_multiplexed_serializes_to_object()
    {
        var entry = new PublishEntry("MyTopic", TopicRoutingMode.NotMultiplexed);

        string json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"NotMultiplexed\"}"));
    }

    [Test]
    public void Correlation_filter_serializes_to_object()
    {
        var entry = new PublishEntry("MyTopic", TopicRoutingMode.CorrelationFilter);

        string json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"CorrelationFilter\"}"));
    }

    [Test]
    public void Sql_filter_serializes_to_object()
    {
        var entry = new PublishEntry("MyTopic", TopicRoutingMode.SqlLikeFilter);

        string json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"SqlLikeFilter\"}"));
    }

    [Test]
    public void Deserializes_correlation_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"CorrelationFilter\"}";

        PublishEntry entry = JsonSerializer.Deserialize<PublishEntry>(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        }
    }

    [Test]
    public void Deserializes_not_multiplexed_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"NotMultiplexed\"}";

        PublishEntry entry = JsonSerializer.Deserialize<PublishEntry>(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.NotMultiplexed));
        }
    }

    [Test]
    public void Deserializes_sql_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"SqlLikeFilter\"}";

        PublishEntry entry = JsonSerializer.Deserialize<PublishEntry>(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.SqlLikeFilter));
        }
    }
}