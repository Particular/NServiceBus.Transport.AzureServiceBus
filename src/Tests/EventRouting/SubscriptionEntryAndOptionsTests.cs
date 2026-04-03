namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System;
using System.Text.Json;
using EventRouting;
using NUnit.Framework;

[TestFixture]
public class SubscriptionEntryTests
{
    [Test]
    public void Implicit_conversion_from_string_creates_catch_all_entry()
    {
        SubscriptionEntry entry = "MyTopic";

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CatchAll));
        });
    }

    [Test]
    public void Can_create_with_explicit_filter_mode()
    {
        var entry = new SubscriptionEntry("MyTopic", SubscriptionFilterMode.CorrelationFilter);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CorrelationFilter));
        });
    }

    [Test]
    public void Default_filter_mode_is_catch_all()
    {
        var entry = new SubscriptionEntry("MyTopic");

        Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CatchAll));
    }

    [Test]
    public void Can_use_all_filter_modes()
    {
        var correlationEntry = new SubscriptionEntry("Topic1", SubscriptionFilterMode.CorrelationFilter);
        var sqlEntry = new SubscriptionEntry("Topic2", SubscriptionFilterMode.SqlFilter);
        var catchAllEntry = new SubscriptionEntry("Topic3", SubscriptionFilterMode.CatchAll);

        Assert.Multiple(() =>
        {
            Assert.That(correlationEntry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CorrelationFilter));
            Assert.That(sqlEntry.FilterMode, Is.EqualTo(SubscriptionFilterMode.SqlFilter));
            Assert.That(catchAllEntry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CatchAll));
        });
    }
}

[TestFixture]
public class MultiplexingOptionsTests
{
    [Test]
    public void Default_values_are_correct()
    {
        var options = new MultiplexingOptions();

        Assert.That(options.Mode, Is.EqualTo(PublishMultiplexingMode.NotMultiplexed));
    }

    [Test]
    public void Can_set_correlation_multiplexing_mode()
    {
        var options = new MultiplexingOptions { Mode = PublishMultiplexingMode.MultiplexedUsingCorrelationFilter };

        Assert.That(options.Mode, Is.EqualTo(PublishMultiplexingMode.MultiplexedUsingCorrelationFilter));
    }

    [Test]
    public void Can_set_sql_multiplexing_mode()
    {
        var options = new MultiplexingOptions { Mode = PublishMultiplexingMode.MultiplexedUsingSqlFilter };

        Assert.That(options.Mode, Is.EqualTo(PublishMultiplexingMode.MultiplexedUsingSqlFilter));
    }
}

[TestFixture]
public class SubscriptionOptionsTests
{
    [Test]
    public void Default_filter_mode_is_catch_all()
    {
        var options = new SubscriptionOptions();

        Assert.That(options.FilterMode, Is.EqualTo(SubscriptionFilterMode.CatchAll));
    }

    [Test]
    public void Can_set_correlation_filter_mode()
    {
        var options = new SubscriptionOptions { FilterMode = SubscriptionFilterMode.CorrelationFilter };

        Assert.That(options.FilterMode, Is.EqualTo(SubscriptionFilterMode.CorrelationFilter));
    }

    [Test]
    public void Can_set_sql_filter_mode()
    {
        var options = new SubscriptionOptions { FilterMode = SubscriptionFilterMode.SqlFilter };

        Assert.That(options.FilterMode, Is.EqualTo(SubscriptionFilterMode.SqlFilter));
    }
}

[TestFixture]
public class SubscriptionEntryJsonConverterTests
{
    [Test]
    public void String_deserializes_to_catch_all_entry()
    {
        const string json = "\"MyTopic\"";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CatchAll));
        });
    }

    [Test]
    public void Catch_all_serializes_to_string()
    {
        var entry = new SubscriptionEntry("MyTopic", SubscriptionFilterMode.CatchAll);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("\"MyTopic\""));
    }

    [Test]
    public void Correlation_filter_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", SubscriptionFilterMode.CorrelationFilter);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"FilterMode\":\"CorrelationFilter\"}"));
    }

    [Test]
    public void Sql_filter_serializes_to_object()
    {
        var entry = new SubscriptionEntry("MyTopic", SubscriptionFilterMode.SqlFilter);

        var json = JsonSerializer.Serialize(entry);

        Assert.That(json, Is.EqualTo("{\"Topic\":\"MyTopic\",\"FilterMode\":\"SqlFilter\"}"));
    }

    [Test]
    public void Deserializes_correlation_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"FilterMode\":\"CorrelationFilter\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.CorrelationFilter));
        });
    }

    [Test]
    public void Deserializes_sql_filter_object()
    {
        const string json = "{\"Topic\":\"MyTopic\",\"FilterMode\":\"SqlFilter\"}";

        var entry = JsonSerializer.Deserialize<SubscriptionEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.FilterMode, Is.EqualTo(SubscriptionFilterMode.SqlFilter));
        });
    }
}
