namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.IO.Hashing;
using System.Text;
using NUnit.Framework;

// Assemblies that share an Azure Service Bus namespace each get a prefix (see AcceptanceTestEntityNames.Prefix.cs) so their entities never collide.
// Fixtures run in parallel and the shared scenarios hard-code some endpoint names, so every fixture also gets its own segment below that prefix.
static partial class AcceptanceTestEntityNames
{
    public static string For(string entityName) => $"{FixtureNamespace()}/{entityName}";

    // For entities that can't live in a namespace but must not be shared between fixtures.
    public static string UniquePerFixture(string entityName) => $"{entityName}-{FixtureToken()}";

    public static void Apply(AzureServiceBusTransport transport) =>
        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = FixtureNamespace() };

    public static HierarchyNamespaceOptions CreateHierarchyNamespaceOptions(string hierarchyNamespace) =>
        new() { HierarchyNamespace = For(hierarchyNamespace) };

    static string FixtureNamespace() => Prefix.Length == 0 ? FixtureToken() : $"{Prefix}/{FixtureToken()}";

    // Lowercase because the dead-letter tests compare entity paths against lowercased endpoint names.
    static string FixtureToken()
    {
        var className = TestContext.CurrentContext.Test.ClassName;
        if (string.IsNullOrEmpty(className))
        {
            throw new InvalidOperationException("Entity names depend on the running fixture and can only be resolved while a test is running.");
        }

        return XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(className)).ToString("x8");
    }
}
