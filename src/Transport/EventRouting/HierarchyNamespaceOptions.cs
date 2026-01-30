namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides options for hierarchy namespace support.
/// </summary>
public sealed class HierarchyNamespaceOptions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator ==(HierarchyNamespaceOptions? left, HierarchyNamespaceOptions? right) => Equals(left, right);

    /// <summary>
    ///
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static bool operator !=(HierarchyNamespaceOptions? left, HierarchyNamespaceOptions? right) => !Equals(left, right);

    bool Equals(HierarchyNamespaceOptions other) => HierarchyNamespace == other.HierarchyNamespace;

    /// <summary>
    ///
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((HierarchyNamespaceOptions)obj);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => HierarchyNamespace.GetHashCode();

    /// <summary>
    /// Defines the hierarchy namespace to be used for entity path prefixing using the format `{HierarchyNamespace}/{entity}`
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    [MemberNotNull(nameof(HierarchyNameSpaceWithTrailingSlash))]
    public required string HierarchyNamespace
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;

            HierarchyNameSpaceWithTrailingSlash = !value.EndsWith('/') ? $"{value}/" : value;
        }
    }

    /// <summary>
    /// The singleton instance representing no hierarchy namespace.
    /// </summary>
    public static HierarchyNamespaceOptions None { get; } = new()
    {
        HierarchyNamespace = string.Empty,
        Locked = true
    };

    bool Locked { get; init; }

    internal string HierarchyNameSpaceWithTrailingSlash { get; init; }

    internal HashSet<string> MessageTypeFullNamesToExclude { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a message type to be excluded from the hierarchy namespace.
    /// </summary>
    /// <typeparam name="TMessageType"></typeparam>
    public void ExcludeMessageType<TMessageType>()
    {
        if (Locked)
        {
            throw new InvalidOperationException("Cannot modify a locked HierarchyNamespaceOptions instance.");
        }

        var fullName = typeof(TMessageType).FullName;
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            MessageTypeFullNamesToExclude.Add(fullName);
        }
    }

    /// <summary>
    /// Adds message types to be excluded from the hierarchy namespace.
    /// </summary>
    /// <param name="messageTypes"></param>
    public void ExcludeMessageTypes(IEnumerable<Type> messageTypes)
    {
        if (Locked)
        {
            throw new InvalidOperationException("Cannot modify a locked HierarchyNamespaceOptions instance.");
        }

        foreach (var messageType in messageTypes)
        {
            if (!string.IsNullOrWhiteSpace(messageType.FullName))
            {
                MessageTypeFullNamesToExclude.Add(messageType.FullName);
            }
        }
    }
}