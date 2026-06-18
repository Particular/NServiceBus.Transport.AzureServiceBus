namespace NServiceBus.Transport.AzureServiceBus.Tests.EventRouting;

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class PublishEntryTypeConverterTests
{
    [Test]
    public void Can_convert_from_string()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["AzureServiceBus:Topology:PublishedEventToTopicsMap:Shared.EventOne"] = "event-one" })
            .Build();

        var options = configuration.GetSection("AzureServiceBus:Topology").Get<TopologyOptions>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.PublishedEventToTopicsMap, Has.Count.EqualTo(1));
            Assert.That(options.PublishedEventToTopicsMap["Shared.EventOne"].Topic, Is.EqualTo("event-one"));
            Assert.That(options.PublishedEventToTopicsMap["Shared.EventOne"].RoutingMode, Is.Null);
        }
    }
}