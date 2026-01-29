namespace NServiceBus.Transport.AzureServiceBus.Tests;

using EventRouting;
using NUnit.Framework;

[TestFixture]
public class HierarchyNamespaceAwareDestinationTests
{
    [Test]
    public void Hierarchy_namespace_destination_generation_is_idempotent()
    {
        var destinationManager = new DestinationManager(new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" });
        const string destination = "Destination";

        var once = destinationManager.GetDestination(destination);
        var twice = destinationManager.GetDestination(once);

        Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
    }
}