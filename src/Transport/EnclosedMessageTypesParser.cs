namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;

static class EnclosedMessageTypesParser
{
    public static string[] Parse(string? enclosedMessageTypes)
    {
        if (string.IsNullOrWhiteSpace(enclosedMessageTypes))
        {
            return [];
        }

        var enclosedMessageTypesSpan = enclosedMessageTypes.AsSpan();
        var normalizedTypes = new List<string>();

        foreach (var messageTypeRange in enclosedMessageTypesSpan.Split(';'))
        {
            var messageTypeSpan = enclosedMessageTypesSpan[messageTypeRange].Trim();
            if (messageTypeSpan.IsEmpty || DoesTypeHaveImplAddedByVersion3(messageTypeSpan))
            {
                continue;
            }

            var normalizedType = GetMessageTypeNameWithoutAssembly(messageTypeSpan);
            if (!normalizedType.IsEmpty)
            {
                normalizedTypes.Add(normalizedType.ToString());
            }
        }

        return [.. normalizedTypes];
    }

    static bool DoesTypeHaveImplAddedByVersion3(ReadOnlySpan<char> existingTypeString) => existingTypeString.IndexOf("__impl".AsSpan(), StringComparison.Ordinal) != -1;

    static ReadOnlySpan<char> GetMessageTypeNameWithoutAssembly(ReadOnlySpan<char> messageTypeIdentifier)
    {
        int lastIndexOf = messageTypeIdentifier.LastIndexOf(']');
        if (lastIndexOf > 0)
        {
            return messageTypeIdentifier[..++lastIndexOf];
        }

        int firstIndexOfComma = messageTypeIdentifier.IndexOf(',');
        if (firstIndexOfComma > 0)
        {
            return messageTypeIdentifier[..firstIndexOfComma];
        }

        return messageTypeIdentifier;
    }
}
