namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using NUnit.Framework;

[TestFixture]
public class FallbackTopicOptionsTests
{
    [Test]
    public void Default_values_are_correct()
    {
        var options = new FallbackTopicOptions();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.TopicName, Is.Null);
            Assert.That(options.RoutingMode, Is.Null);
        }
    }

    [Test]
    public void Can_set_topic_name_and_mode()
    {
        var options = new FallbackTopicOptions { TopicName = "SharedTopic", RoutingMode = TopicRoutingMode.CorrelationFilter };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.TopicName, Is.EqualTo("SharedTopic"));
            Assert.That(options.RoutingMode, Is.EqualTo(TopicRoutingMode.CorrelationFilter));
        }
    }
}