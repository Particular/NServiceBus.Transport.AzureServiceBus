namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System;
using System.Text.Json;
using EventRouting;
using NUnit.Framework;

[TestFixture]
public class SubscriptionEntryTests
{
    [Test]
    public void Implicit_conversion_from_string_creates_default_entry()
    {
        SubscriptionEntry entry = "MyTopic";

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.Default));
        });
    }

    [Test]
    public void Can_create_with_explicit_routing_mode()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.CorrelationFilter);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        });
    }

    [Test]
    public void Default_routing_mode_is_default()
    {
        var entry = new SubscriptionEntry("MyTopic");

        Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.Default));
    }

    [Test]
    public void Can_use_all_routing_modes()
    {
        var notMultiplexedEntry = new SubscriptionEntry("Topic1", TopicRoutingMode.NotMultiplexed);
        var correlationEntry = new SubscriptionEntry("Topic2", TopicRoutingMode.CorrelationFilter);
        var sqlEntry = new SubscriptionEntry("Topic3", TopicRoutingMode.SqlFilter);
        var catchAllEntry = new SubscriptionEntry("Topic4", TopicRoutingMode.CatchAll);
        var defaultEntry = new SubscriptionEntry("Topic5", TopicRoutingMode.Default);

        Assert.Multiple(() =>
        {
            Assert.That(notMultiplexedEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.NotMultiplexed));
            Assert.That(correlationEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
            Assert.That(sqlEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.SqlFilter));
            Assert.That(catchAllEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.CatchAll));
            Assert.That(defaultEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.Default));
        });
    }
}

[TestFixture]
public class RoutingOptionsTests
{
    [Test]
    public void Default_values_are_correct()
    {
        var options = new RoutingOptions();

        Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.Default));
    }

    [Test]
    public void Can_set_correlation_routing_mode()
    {
        var options = new RoutingOptions { Mode = TopicRoutingMode.CorrelationFilter };

        Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
    }

    [Test]
    public void Can_set_sql_routing_mode()
    {
        var options = new RoutingOptions { Mode = TopicRoutingMode.SqlFilter };

        Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.SqlFilter));
    }
}

[TestFixture]
public class FallbackTopicOptionsTests
{
    [Test]
    public void Default_values_are_correct()
    {
        var options = new FallbackTopicOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.TopicName, Is.Null);
            Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.Default));
        });
    }

    [Test]
    public void Can_set_topic_name_and_mode()
    {
        var options = new FallbackTopicOptions { TopicName = "SharedTopic", Mode = TopicRoutingMode.CorrelationFilter };

        Assert.Multiple(() =>
        {
            Assert.That(options.TopicName, Is.EqualTo("SharedTopic"));
            Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        });
    }
}

[TestFixture]
public class SubscriptionEntryJsonConverterTests
{
    [Test]
    public void String_deserializes_to_default_entry()
    {
        const string json = "\"MyTopic\"";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.Default));
        });
    }

    [Test]
    public void Default_serializes_to_string()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.Default);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("\"MyTopic\""));
    }

    [Test]
    public void Topic_only_string_form_round_trips_without_changing_shape()
    {
        const string json = "\"MyTopic\"";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);
        var serialized = JsonSerializer.Serialize(entry);

        Assert.That(serialized, Is.EqualTo(json));
    }

    [Test]
    public void Catch_all_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.CatchAll);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"CatchAll\"}"));
    }

    [Test]
    public void Not_multiplexed_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.NotMultiplexed);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"NotMultiplexed\"}"));
    }

    [Test]
    public void Correlation_filter_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.CorrelationFilter);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"CorrelationFilter\"}"));
    }

    [Test]
    public void Sql_filter_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.SqlFilter);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"RoutingMode\":\"SqlFilter\"}"));
    }

    [Test]
    public void Deserializes_correlation_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"CorrelationFilter\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        });
    }

    [Test]
    public void Deserializes_not_multiplexed_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"NotMultiplexed\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.NotMultiplexed));
        });
    }

    [Test]
    public void Deserializes_sql_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"SqlFilter\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.SqlFilter));
        });
    }

    [Test]
    public void Deserializes_default_routing_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"RoutingMode\":\"Default\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.Default));
        });
    }

}
