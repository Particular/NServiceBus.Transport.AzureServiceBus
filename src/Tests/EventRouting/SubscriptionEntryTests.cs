namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using NUnit.Framework;

[TestFixture]
public class SubscriptionEntryTests
{
    [Test]
    public void Implicit_conversion_from_string_creates_default_entry()
    {
        SubscriptionEntry entry = "MyTopic";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.Null);
        }
    }

    [Test]
    public void Can_create_with_explicit_routing_mode()
    {
        var entry = new SubscriptionEntry("MyTopic", TopicRoutingMode.CorrelationFilter);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Topic, Is.EqualTo("MyTopic"));
            Assert.That(entry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        }
    }

    [Test]
    public void Default_routing_mode_is_null()
    {
        var entry = new SubscriptionEntry("MyTopic");

        Assert.That(entry.RoutingMode, Is.Null);
    }

    [Test]
    public void Can_use_all_routing_modes()
    {
        var notMultiplexedEntry = new SubscriptionEntry("Topic1", TopicRoutingMode.NotMultiplexed);
        var correlationEntry = new SubscriptionEntry("Topic2", TopicRoutingMode.CorrelationFilter);
        var sqlEntry = new SubscriptionEntry("Topic3", TopicRoutingMode.SqlLikeFilter);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notMultiplexedEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.NotMultiplexed));
            Assert.That(correlationEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
            Assert.That(sqlEntry.RoutingMode, Is.EqualTo(TopicRoutingMode.SqlLikeFilter));
        }
    }
}