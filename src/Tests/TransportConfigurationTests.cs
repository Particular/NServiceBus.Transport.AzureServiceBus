namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using NUnit.Framework;

[TestFixture]
public class TransportConfigurationTests
{
    [Test]
    [TestCase(
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://192.168.1.1;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://192.168.1.1:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://servicebus-emulator:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://host.docker.internal;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://host.docker.internal:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://localhost:5300/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://192.168.1.1/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://192.168.1.1:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://servicebus-emulator/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://servicebus-emulator:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    [TestCase(
        "Endpoint=sb://host.docker.internal/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
        "Endpoint=sb://host.docker.internal:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;")]
    public void InjectEmulatorAdminPort_should_append_port_5300_when_missing(string input, string expected) =>
        Assert.That(AzureServiceBusTransport.InjectEmulatorAdminPort(input), Is.EqualTo(expected));

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