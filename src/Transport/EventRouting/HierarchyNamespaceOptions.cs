namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides options for hierarchy namespace support.
/// </summary>
public sealed class HierarchyNamespaceOptions
{
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

    internal void ValidateDestinations(IEnumerable<string> destinations)
    {
        var validationResult = EntityValidator.ValidateQueues(destinations, nameof(ValidateDestinations), this);
        if (validationResult != ValidationResult.Success)
        {
            var message = validationResult?.ErrorMessage ?? "One or more queue names do not comply with the Azure Service Bus queue limits.";
            throw new ValidationException(message);
        }
    }

    /// <summary>
    /// Indicates whether two HierarchyNamespaceOptions objects are equal.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>true if left is equal to right; otherwise, false.</returns>
    public static bool operator ==(HierarchyNamespaceOptions? left, HierarchyNamespaceOptions? right) => Equals(left, right);

    /// <summary>
    /// Indicates whether two HierarchyNamespaceOptions objects are equal.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>true if left is not equal to right; otherwise, false.</returns>
    public static bool operator !=(HierarchyNamespaceOptions? left, HierarchyNamespaceOptions? right) => !Equals(left, right);

    bool Equals(HierarchyNamespaceOptions other) => HierarchyNamespace == other.HierarchyNamespace;

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
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
    /// Returns the hash code for this string.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HierarchyNamespace.GetHashCode();

    bool Locked { get; init; }

    internal string HierarchyNameSpaceWithTrailingSlash { get; init; }

    internal HashSet<string> MessageTypeFullNamesToExclude { get; } = new(StringComparer.Ordinal);
}