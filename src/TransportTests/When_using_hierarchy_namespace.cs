namespace NServiceBus.Transport.AzureServiceBus.TransportTests;

using System;
using System.ComponentModel.DataAnnotations;
using NUnit.Framework;
using NServiceBus.TransportTests;

[TestFixture]
public class When_using_hierarchy_namespace : NServiceBusTransportTest
{
    [Test]
    public void Should_throw_when_hierarchy_namespace_plus_queue_name_too_long()
    {
        var transport = (AzureServiceBusTransport)new ConfigureAzureServiceBusTransportInfrastructure().CreateTransportDefinition();
        transport.HierarchyNamespace = new string('a', 200);

        var hostSettings = new HostSettings(
            "WhenUsingHierarchyNamespace",
            string.Empty,
            new StartupDiagnosticEntries(),
            (_, _, _) => { },
            true);

        var receivers = new[]
        {
            new ReceiveSettings("1", new QueueAddress(new string('q', 100)), true, false, hostSettings.Name + ".error")
        };

        Assert.That(async () => await transport.Initialize(hostSettings, receivers, Array.Empty<string>()),
            Throws.TypeOf<ValidationException>());
    }

    [Test]
    public void Should_throw_when_hierarchy_namespace_contains_invalid_characters()
    {
        var transport = (AzureServiceBusTransport)new ConfigureAzureServiceBusTransportInfrastructure().CreateTransportDefinition();
        transport.HierarchyNamespace = "bad?prefix";

        var hostSettings = new HostSettings(
            "WhenUsingHierarchyNamespace",
            string.Empty,
            new StartupDiagnosticEntries(),
            (_, _, _) => { },
            true);

        var receivers = new[]
        {
            new ReceiveSettings("1", new QueueAddress("validqueue"), true, false, hostSettings.Name + ".error")
        };

        Assert.That(async () => await transport.Initialize(hostSettings, receivers, Array.Empty<string>()),
            Throws.TypeOf<ValidationException>());
    }
}