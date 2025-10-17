namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using NUnit.Framework;

[TestFixture]
public class TransportConfigurationTests
{
    [Test]
    [TestCase(null)] // default
    [TestCase(0)] // no prefetch
    [TestCase(1)] // arbitrary
    [TestCase(100)] // arbitrary
    public void PrefetchCount_should_allow_valid_values(int? prefetchCount)
    {
        var transport =
            new AzureServiceBusTransport("connectionString", TopicTopology.Default)
            {
                PrefetchCount = prefetchCount
            };

        Assert.That(transport.PrefetchCount, Is.EqualTo(prefetchCount));
    }

    [Test]
    [TestCase(-1)]
    [TestCase(-100)]
    public void PrefetchCount_should_disallow_negative(int prefetchCount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new AzureServiceBusTransport("connectionString", TopicTopology.Default)
            {
                PrefetchCount = prefetchCount
            };
        });
}