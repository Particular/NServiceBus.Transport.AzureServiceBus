namespace NServiceBus.Transport.AzureServiceBus.Tests;

using AzureServiceBus.Sending;
using NUnit.Framework;

[TestFixture]
public class HierarchyNamespaceAwareDestinationTests
{
    [Test]
    public void Hierarchy_namespace_aware_destination_generation_is_idempotent()
    {
        var hierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        const string destination = "Destination";

        var once = destination.ToHierarchyNamespaceAwareDestination(hierarchyNamespaceOptions);
        var twice = once.ToHierarchyNamespaceAwareDestination(hierarchyNamespaceOptions);

        Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
    }
}