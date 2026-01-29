namespace NServiceBus.Transport.AzureServiceBus.EventRouting;

using System;
using System.Collections.Concurrent;
using System.Linq;

class DestinationManager(HierarchyNamespaceOptions? options)
{
    internal string GetDestination(string baseDestination, string? messageTypeFullName = null) =>
        GetDestination(baseDestination, string.IsNullOrWhiteSpace(messageTypeFullName) ? [] : [messageTypeFullName]);

    internal string GetDestination(string destination, string[] messageTypeFullNames) =>
        destinationMessageTypesToHierarchyNamespaceDestination.GetOrAdd((destination, messageTypeFullNames),
            static (key, hierarchyNamespaceOptions) =>
            {
                var (destination, messageTypeFullNames) = key;
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
            }, options);

    readonly ConcurrentDictionary<(string, string[]), string> destinationMessageTypesToHierarchyNamespaceDestination = new();
}