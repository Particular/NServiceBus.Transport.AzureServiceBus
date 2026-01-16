namespace NServiceBus.Transport.AzureServiceBus.Sending;

static class HierarchyNamespaceExtensions
{
    internal static string ToHierarchyNamespaceAwareDestination(this string destination, string? hierarchyNamespace)
    {
        if (hierarchyNamespace is null || destination.StartsWith(hierarchyNamespace))
        {
            return destination;
        }

        return $"{hierarchyNamespace}/{destination}";
    }
}