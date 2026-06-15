namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class EnclosedMessageTypesParserTests
{
    [Test]
    public void Should_strip_assembly_qualification_and_preserve_nested_type_names()
    {
        var enclosedMessageTypes = typeof(NestedMessage).AssemblyQualifiedName;

        var normalizedTypes = EnclosedMessageTypesParser.Parse(enclosedMessageTypes);

        Assert.That(normalizedTypes, Is.EqualTo(new[] { typeof(NestedMessage).FullName }));
    }

    [Test]
    public void Should_ignore_proxy_impl_entries_and_trim_whitespace()
    {
        var proxyType = $"{typeof(ConcreteMessage).FullName}__impl";
        var assemblyName = typeof(ConcreteMessage).Assembly.GetName().Name;
        var enclosedMessageTypes = $"  {proxyType}, {assemblyName} ; {typeof(IMessageContract).AssemblyQualifiedName}  ";

        var normalizedTypes = EnclosedMessageTypesParser.Parse(enclosedMessageTypes);

        Assert.That(normalizedTypes, Is.EqualTo(new[] { typeof(IMessageContract).FullName }));
    }

    [Test]
    public void Should_strip_assembly_qualification_from_generic_types()
    {
        var genericMessageType = typeof(GenericMessage<NestedMessage>);

        var normalizedTypes = EnclosedMessageTypesParser.Parse(genericMessageType.AssemblyQualifiedName);

        Assert.That(normalizedTypes, Is.EqualTo(new[] { genericMessageType.FullName }));
    }

    [Test]
    public void Should_parse_multiple_semicolon_separated_entries()
    {
        var enclosedMessageTypes = $"{typeof(NestedMessage).AssemblyQualifiedName};{typeof(AnotherNestedMessage).AssemblyQualifiedName}";

        var normalizedTypes = EnclosedMessageTypesParser.Parse(enclosedMessageTypes);

        Assert.That(normalizedTypes, Is.EqualTo(new[] { typeof(NestedMessage).FullName, typeof(AnotherNestedMessage).FullName }));
    }

    class NestedMessage;
    class AnotherNestedMessage;
    class ConcreteMessage;
    interface IMessageContract;
    class GenericMessage<T>;
}
