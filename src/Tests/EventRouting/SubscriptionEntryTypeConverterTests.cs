namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class SubscriptionEntryTypeConverterTests
{
    [Test]
    public void Can_convert_from_string()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne:0"] = "event-one" })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"], Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().Topic, Is.EqualTo("event-one"));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().RoutingMode, Is.Null);
        }
    }

    [Theory]
    [TestCase("NotMultiplexed", TopicRoutingMode.NotMultiplexed)]
    [TestCase("CorrelationFilter", TopicRoutingMode.CorrelationFilter)]
    [TestCase("SqlLikeFilter", TopicRoutingMode.SqlLikeFilter)]
    public void Can_convert_from_subscription_entry(string routingMode, TopicRoutingMode expectedRoutingMode)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne:0:Topic"] = "event-one",
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne:0:RoutingMode"] = routingMode
            })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"], Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().Topic, Is.EqualTo("event-one"));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().RoutingMode, Is.EqualTo(expectedRoutingMode));
        }
    }
}