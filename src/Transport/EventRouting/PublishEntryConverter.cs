namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class PublishEntryConverter : JsonConverter<PublishEntry>
{
    public override PublishEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string topic = reader.GetString() ?? throw new JsonException("Topic cannot be null");
            return new PublishEntry(topic);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected string or StartObject token");
        }

        string? topicName = null;
        TopicRoutingMode? mode = null;

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
                case "RoutingMode":
                    mode = Enum.Parse<TopicRoutingMode>(reader.GetString() ?? throw new JsonException("Mode cannot be null"));
                    break;
                default:
                    break;
            }
        }

        return topicName is null ? throw new JsonException("Topic is required") : new PublishEntry(topicName, mode);
    }

    public override void Write(Utf8JsonWriter writer, PublishEntry value, JsonSerializerOptions options)
    {
        if (value.RoutingMode is null)
        {
            writer.WriteStringValue(value.Topic);
        }
        else
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Topic");
            writer.WriteStringValue(value.Topic);
            writer.WritePropertyName("RoutingMode");
            writer.WriteStringValue(value.RoutingMode.Value.ToString());
            writer.WriteEndObject();
        }
    }
}