namespace NServiceBus.Transport.AzureServiceBus.Tests;

using AzureServiceBus.Sending;
using NUnit.Framework;

[TestFixture]
public class HierarchyNamespaceAwareDestinationTests
{
    [Test]
    public void Default_queue_name_generator_is_idempotent()
    {
        const string prefix = "Prefix";
        const string destination = "Destination";

        var once = destination.ToHierarchyNamespaceAwareDestination(prefix);
        var twice = once.ToHierarchyNamespaceAwareDestination(prefix);

        Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
    }
}