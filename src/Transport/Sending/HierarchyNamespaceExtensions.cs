namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;

static class HierarchyNamespaceExtensions
{
    internal static string ToHierarchyNamespaceAwareDestination(this string destination, string? hierarchyNamespace)
    {
        var hierarchyNamespaceSpan = hierarchyNamespace.AsSpan();
        var destinationSpan = destination.AsSpan();
        if (hierarchyNamespaceSpan.IsEmpty || destinationSpan.StartsWith(hierarchyNamespaceSpan, StringComparison.Ordinal))
        {
            return destination;
        }

        return string.Create(hierarchyNamespaceSpan.Length + 1 + destinationSpan.Length, (hierarchyNamespace, destination), static (chars, state) =>
        {
            var position = 0;
            (string? hierarchyNamespace, string destination) = state;
            hierarchyNamespace.AsSpan().CopyTo(chars);
            position += hierarchyNamespace?.Length ?? 0;
            chars[position++] = '/';
            destination.AsSpan().CopyTo(chars.Slice(position));
        });
    }
}