namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class SubscriptionEntryConverter : JsonConverter<SubscriptionEntry>
{
    public override SubscriptionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string topic = reader.GetString() ?? throw new JsonException("Topic cannot be null");
            return new SubscriptionEntry(topic, SubscriptionFilterMode.Default);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected string or StartObject token");
        }

        string? topicName = null;
        SubscriptionFilterMode filterMode = SubscriptionFilterMode.Default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            string propertyName = reader.GetString() ?? throw new JsonException("Property name cannot be null");
            _ = reader.Read();

            switch (propertyName)
            {
                case "Topic":
                    topicName = reader.GetString() ?? throw new JsonException("Topic cannot be null");
                    break;
                case "FilterMode":
                    filterMode = Enum.Parse<SubscriptionFilterMode>(reader.GetString() ?? throw new JsonException("FilterMode cannot be null"));
                    break;
                default:
                    break;
            }
        }

        if (topicName is null)
        {
            throw new JsonException("Topic is required");
        }

        return new SubscriptionEntry(topicName, filterMode);
    }

    public override void Write(Utf8JsonWriter writer, SubscriptionEntry value, JsonSerializerOptions options)
    {
        if (value.FilterMode == SubscriptionFilterMode.Default)
        {
            writer.WriteStringValue(value.Topic);
        }
        else
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Topic");
            writer.WriteStringValue(value.Topic);
            writer.WritePropertyName("FilterMode");
            writer.WriteStringValue(value.FilterMode.ToString());
            writer.WriteEndObject();
        }
    }
}
