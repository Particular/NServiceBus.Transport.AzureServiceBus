namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

// Assemblies that share an Azure Service Bus namespace each get a prefix (see AcceptanceTestEntityNames.Prefix.cs) so their entities never collide.
static partial class AcceptanceTestEntityNames
{
    public static string For(string entityName) => Prefix.Length == 0 ? entityName : $"{Prefix}/{entityName}";

    public static void Apply(AzureServiceBusTransport transport)
    {
        if (Prefix.Length > 0)
        {
            transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = Prefix };
        }
    }

    public static HierarchyNamespaceOptions CreateHierarchyNamespaceOptions(string hierarchyNamespace) =>
        new() { HierarchyNamespace = For(hierarchyNamespace) };
}
