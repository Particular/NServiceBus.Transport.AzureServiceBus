namespace NServiceBus.Transport.AzureServiceBus;

using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a publish entry with topic and routing mode information.
/// </summary>
[JsonConverter(typeof(PublishEntryConverter))]
[TypeConverter(typeof(PublishEntryTypeConverter))]
public readonly record struct PublishEntry(string Topic, TopicRoutingMode? Mode = null)
{
    /// <summary>
    /// Implicitly converts a string to a PublishEntry with no routing mode specified.
    /// </summary>
    public static implicit operator PublishEntry(string topic) => new(topic);
}
