namespace NServiceBus.Transport.AzureServiceBus.Tests.Configuration;

using System;
using NUnit.Framework;

[TestFixture]
public class Validate_AutoDeleteOnIdle_Prop
{
    [Test]
    public void AutoDeleteOnIdle_Should_accept_valid_timespan()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default);

        var validTimeSpan = TimeSpan.FromMinutes(10);

        Assert.DoesNotThrow(() => transport.AutoDeleteOnIdle = validTimeSpan);
        Assert.That(transport.AutoDeleteOnIdle, Is.EqualTo(validTimeSpan));
    }

    [Test]
    public void AutoDeleteOnIdle_Should_accept_null()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default);

        Assert.DoesNotThrow(() => transport.AutoDeleteOnIdle = null);
        Assert.That(transport.AutoDeleteOnIdle, Is.Null);
    }

    [Test]
    public void AutoDeleteOnIdle_Should_throw_when_less_than_minimum()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default);

        var invalidTimeSpan = TimeSpan.FromMinutes(4);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => transport.AutoDeleteOnIdle = invalidTimeSpan);
        Assert.That(exception.ParamName, Is.EqualTo("AutoDeleteOnIdle"));
    }

    [Test]
    public void AutoDeleteOnIdle_Should_accept_minimum_value()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default);

        var minimumTimeSpan = TimeSpan.FromMinutes(5);

        Assert.DoesNotThrow(() => transport.AutoDeleteOnIdle = minimumTimeSpan);
        Assert.That(transport.AutoDeleteOnIdle, Is.EqualTo(minimumTimeSpan));
    }
}
