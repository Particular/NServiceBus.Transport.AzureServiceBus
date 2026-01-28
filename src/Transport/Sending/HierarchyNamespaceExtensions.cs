namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Linq;

static class HierarchyNamespaceExtensions
{
    internal static string ToHierarchyNamespaceAwareDestination(this string destination, HierarchyNamespaceOptions? hierarchyNamespaceOptions, string? messageTypeFullName = null)
        => destination.ToHierarchyNamespaceAwareDestination(hierarchyNamespaceOptions, string.IsNullOrWhiteSpace(messageTypeFullName) ? [] : [messageTypeFullName]);
    internal static string ToHierarchyNamespaceAwareDestination(this string destination, HierarchyNamespaceOptions? hierarchyNamespaceOptions, string[] messageTypeFullNames)
    {
        if (string.IsNullOrWhiteSpace(hierarchyNamespaceOptions?.HierarchyNamespace))
        {
            return destination;
        }

        if (messageTypeFullNames is { Length: > 0 } && hierarchyNamespaceOptions.MessageTypeFullNamesToExclude.Any(messageTypeFullNames.Contains))
        {
            return destination;
        }

        var hierarchyNamespace = hierarchyNamespaceOptions.HierarchyNamespace;
        var hierarchyNamespaceSpan = string.Concat(hierarchyNamespace, '/').AsSpan();
        var destinationSpan = destination.AsSpan();

        if (destinationSpan.StartsWith(hierarchyNamespaceSpan, StringComparison.Ordinal))
        {
            return destination;
        }

        return string.Create(hierarchyNamespace.Length + 1 + destination.Length, (hierarchyNamespace, destination), static (chars, state) =>
        {
            var position = 0;
            (string? hierarchyNamespace, string destination) = state;
            hierarchyNamespace.AsSpan().CopyTo(chars);
            position += hierarchyNamespace.Length;
            chars[position++] = '/';
            destination.AsSpan().CopyTo(chars.Slice(position));
        });
    }
}