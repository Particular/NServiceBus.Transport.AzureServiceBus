namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a subscription entry with topic and filter mode information.
/// </summary>
[JsonConverter(typeof(SubscriptionEntryConverter))]
public readonly record struct SubscriptionEntry(string Topic, SubscriptionFilterMode FilterMode = SubscriptionFilterMode.CatchAll)
{
    /// <summary>
    /// Implicitly converts a string to a SubscriptionEntry with CatchAll filter mode.
    /// </summary>
    public static implicit operator SubscriptionEntry(string topic) => new(topic, SubscriptionFilterMode.CatchAll);
}