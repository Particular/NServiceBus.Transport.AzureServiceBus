namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;

/// <summary>
/// Provides options for hierarchy namespace support.
/// </summary>
public class HierarchyNamespaceOptions
{
    /// <summary>
    /// Defines the hierarchy namespace to be used for entity path prefixing using the format `{HierarchyNamespace}/{entity}`
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public required string HierarchyNamespace
    {
        get;
        init
        {
            if (value.EndsWith('/'))
            {
                throw new ArgumentException($"{nameof(HierarchyNamespace)} cannot end with `/`", nameof(HierarchyNamespace));
            }
            field = value;
        }
    }
    internal HashSet<Type> MessageTypesToExclude { get; } = [];

    /// <summary>
    /// Adds a message type to be excluded from the hierarchy namespace.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    public void ExcludeMessageType<TMessageType>() => MessageTypesToExclude.Add(typeof(TMessageType));

    /// <summary>
    /// Adds message types to be excluded from the hierarchy namespace.
    /// </summary>
    /// <param name="messageTypes"></param>
    public void ExcludeMessageTypes(IEnumerable<Type> messageTypes)
    {
        foreach (var messageType in messageTypes)
        {
            MessageTypesToExclude.Add(messageType);
        }
    }
}