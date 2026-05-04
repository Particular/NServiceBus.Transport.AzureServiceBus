namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a subscription entry with topic and routing mode information.
/// </summary>
[JsonConverter(typeof(SubscriptionEntryConverter))]
public readonly record struct SubscriptionEntry(string Topic, TopicRoutingMode RoutingMode = TopicRoutingMode.Default)
{
    /// <summary>
    /// Implicitly converts a string to a SubscriptionEntry with Default routing mode.
    /// </summary>
    public static implicit operator SubscriptionEntry(string topic) => new(topic, TopicRoutingMode.Default);
}
