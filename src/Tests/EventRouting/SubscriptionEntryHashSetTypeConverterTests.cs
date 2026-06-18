namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class SubscriptionEntryHashSetTypeConverterTests
{
    [Test]
    public void Can_convert_single_string_to_single_element_set()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne"] = "event-one"
            })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"], Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().Topic, Is.EqualTo("event-one"));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().RoutingMode, Is.Null);
        }
    }

    [Test]
    public void Can_convert_multiple_strings_to_multi_element_set()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne:0"] = "event-one",
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne:1"] = "event-two"
            })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SubscribedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"], Has.Count.EqualTo(2));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].Select(e => e.Topic),
                Is.EquivalentTo(["event-one", "event-two"]));
        }
    }

    [Test]
    public void Can_convert_mixed_single_and_multiple_entries()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventOne"] = "event-one",
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventTwo:0"] = "event-two-a",
                ["AzureServiceBus:Topology:SubscribedEventToTopicsMap:Shared.EventTwo:1"] = "event-two-b"
            })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SubscribedEventToTopicsMap, Has.Count.EqualTo(2));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"], Has.Count.EqualTo(1));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventOne"].First().Topic, Is.EqualTo("event-one"));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventTwo"], Has.Count.EqualTo(2));
            Assert.That(options.SubscribedEventToTopicsMap["Shared.EventTwo"].Select(e => e.Topic),
                Is.EquivalentTo(["event-two-a", "event-two-b"]));
        }
    }
}