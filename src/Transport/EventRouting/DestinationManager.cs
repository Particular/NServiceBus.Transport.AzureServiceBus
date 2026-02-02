namespace NServiceBus.Transport.AzureServiceBus.EventRouting;

using System;
using System.Collections.Concurrent;

class DestinationManager(HierarchyNamespaceOptions options)
{
    internal string GetDestination(string destination, string? enclosedMessageTypes = null)
    {
        if (options == HierarchyNamespaceOptions.None || IsAnyEnclosedMessageTypeExcluded(enclosedMessageTypes))
        {
            return destination;
        }

        var hierarchyNamespaceSpan = options.HierarchyNameSpaceWithTrailingSlash;
        var destinationSpan = destination.AsSpan();

        if (destinationSpan.StartsWith(hierarchyNamespaceSpan, StringComparison.Ordinal))
        {
            return destination;
        }

        return string.Create(options.HierarchyNameSpaceWithTrailingSlash.Length + destination.Length, (options.HierarchyNameSpaceWithTrailingSlash, destination), static (chars, state) =>
        {
            var position = 0;
            (string hierarchyNamespace, string destination) = state;
            hierarchyNamespace.AsSpan().CopyTo(chars);
            position += hierarchyNamespace.Length;
            destination.AsSpan().CopyTo(chars[position..]);
        });
    }

    bool IsAnyEnclosedMessageTypeExcluded(string? enclosedMessageTypes)
    {
        if (string.IsNullOrWhiteSpace(enclosedMessageTypes))
        {
            return false;
        }

        return enclosedMessageTypesToExcluded.GetOrAdd(enclosedMessageTypes,
            static (key, hierarchyNamespaceOptions) =>
            {
                var enclosedMessageTypesSpan = key.AsSpan();

                foreach (var messageTypeRange in enclosedMessageTypesSpan.Split(EnclosedMessageTypeSeparator))
                {
                    var messageTypeSpan = enclosedMessageTypesSpan[messageTypeRange].Trim();

                    int lastIndexOf = messageTypeSpan.LastIndexOf(']');
                    int firstIndexOfComma = messageTypeSpan.IndexOf(',');
                    if (lastIndexOf > 0)
                    {
                        messageTypeSpan = messageTypeSpan[..lastIndexOf];
                    }
                    else if (firstIndexOfComma > 0)
                    {
                        messageTypeSpan = messageTypeSpan[..firstIndexOfComma];
                    }

                    var alternativeLookup = hierarchyNamespaceOptions.MessageTypeFullNamesToExclude.GetAlternateLookup<ReadOnlySpan<char>>();
                    if (alternativeLookup.Contains(messageTypeSpan))
                    {
                        return true;
                    }
                }

                return false;
            }, options);
    }

    static ReadOnlySpan<char> EnclosedMessageTypeSeparator => ";".AsSpan();
    readonly ConcurrentDictionary<string, bool> enclosedMessageTypesToExcluded = new();
}