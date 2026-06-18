namespace NServiceBus.Transport.AzureServiceBus;

using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a subscription entry with topic and routing mode information.
/// </summary>
[JsonConverter(typeof(SubscriptionEntryConverter))]
[TypeConverter(typeof(SubscriptionEntryTypeConverter))]
public readonly record struct SubscriptionEntry(string Topic, TopicRoutingMode? RoutingMode = null)
{
    /// <summary>
    /// Implicitly converts a string to a SubscriptionEntry with no routing mode specified.
    /// </summary>
    public static implicit operator SubscriptionEntry(string topic) => new(topic);
}