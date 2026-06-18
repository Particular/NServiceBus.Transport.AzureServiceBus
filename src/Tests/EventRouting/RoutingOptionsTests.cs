namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using NUnit.Framework;

[TestFixture]
public class RoutingOptionsTests
{
    [Test]
    public void Default_values_are_correct()
    {
        var options = new RoutingOptions();

        Assert.That(options.Mode, Is.Null);
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
        var options = new RoutingOptions { Mode = TopicRoutingMode.SqlLikeFilter };

        Assert.That(options.Mode, Is.EqualTo(TopicRoutingMode.SqlLikeFilter));
    }
}