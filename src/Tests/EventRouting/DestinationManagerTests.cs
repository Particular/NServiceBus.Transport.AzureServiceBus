namespace NServiceBus.Transport.AzureServiceBus.Tests;

using EventRouting;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;

[TestFixture]
public class DestinationManagerTests
{
    [Test]
    public void Should_exclude_assembly_qualified_message_type()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        options.ExcludeMessageType<MyMessage>();
        var destinationManager = new DestinationManager(options);

        var destination = destinationManager.GetDestination("Destination", typeof(MyMessage).AssemblyQualifiedName);

        Assert.That(destination, Is.EqualTo("Destination"));
    }

    [Test]
    public void Should_exclude_semicolon_separated_message_types_with_whitespace()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        options.ExcludeMessageType<MyMessage>();
        var destinationManager = new DestinationManager(options);
        var enclosedMessageTypes = $"  {typeof(OtherMessage).AssemblyQualifiedName} ; {typeof(MyMessage).AssemblyQualifiedName}  ";

        var destination = destinationManager.GetDestination("Destination", enclosedMessageTypes);

        Assert.That(destination, Is.EqualTo("Destination"));
    }

    [Test]
    public void Should_ignore_impl_entries_when_checking_exclusions()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        options.ExcludeMessageType<IContract>();
        var destinationManager = new DestinationManager(options);
        var proxyType = $"{typeof(ImplMessage).FullName}__impl";
        var assemblyName = typeof(ImplMessage).Assembly.GetName().Name;
        var enclosedMessageTypes = $"{proxyType}, {assemblyName};{typeof(IContract).AssemblyQualifiedName}";

        var destination = destinationManager.GetDestination("Destination", enclosedMessageTypes);

        Assert.That(destination, Is.EqualTo("Destination"));
    }

    [Test]
    public void Should_exclude_generic_message_types()
    {
        var options = new HierarchyNamespaceOptions { HierarchyNamespace = "Prefix" };
        options.ExcludeMessageType<GenericMessage<MyMessage>>();
        var destinationManager = new DestinationManager(options);

        var destination = destinationManager.GetDestination("Destination", typeof(GenericMessage<MyMessage>).AssemblyQualifiedName);

        Assert.That(destination, Is.EqualTo("Destination"));
    }

    class MyMessage;
    class OtherMessage;
    class ImplMessage;
    interface IContract;
    class GenericMessage<T>;
}
