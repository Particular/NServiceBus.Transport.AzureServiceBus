namespace NServiceBus.Transport.AzureServiceBus.Tests;

using EventRouting;
using NUnit.Framework;

[TestFixture]
public class HierarchyNamespaceAwareDestinationTests
{
    [Test]
    public void Hierarchy_namespace_destination_generation_is_idempotent()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        var destinationManager = new DestinationManager(options);
        const string destination = "Destination";

        var once = destinationManager.GetDestination(destination);
        var twice = destinationManager.GetDestination(once);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
            Assert.That(once, Does.StartWith(options.HierarchyNameSpaceWithTrailingSlash), "Excluded message types should still have the hierarchy namespace applied");
        }
    }

    [Test]
    public void Hierarchy_namespace_destination_generation_is_idempotent_wth_exclusion()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        options.ExcludeMessageType<MyMessage>();
        var destinationManager = new DestinationManager(options);
        const string destination = "Destination";

        var once = destinationManager.GetDestination(destination, typeof(MyMessage).FullName);
        var twice = destinationManager.GetDestination(once, typeof(MyMessage).FullName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
            Assert.That(once, Does.Not.StartWith(options.HierarchyNameSpaceWithTrailingSlash), "Excluded message types should still have the hierarchy namespace applied");
        }
    }

    [Test]
    public void No_hierarchy_namespace_destination_generation_is_idempotent()
    {
        var options = HierarchyNamespaceOptions.None;
        var destinationManager = new DestinationManager(options);
        const string destination = "Destination";

        var once = destinationManager.GetDestination(destination);
        var twice = destinationManager.GetDestination(once);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
            Assert.That(once, Does.Not.Contain("/"), "Excluded message types should still have the hierarchy namespace applied");
        }
    }

    class MyMessage;
}