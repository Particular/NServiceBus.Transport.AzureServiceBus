namespace NServiceBus.Transport.AzureServiceBus;

interface IHierarchyNamespaceAwareOptions
{
    internal HierarchyNamespaceOptions HierarchyNamespaceOptions { get; set; }
}