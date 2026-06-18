namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class PublishedEventToTopicsMapConverter : JsonConverter<Dictionary<string, PublishEntry>>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(Dictionary<string, PublishEntry>) ||
        typeToConvert == typeof(Dictionary<string, string>);

    public override Dictionary<string, PublishEntry> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var map = new Dictionary<string, PublishEntry>();

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

            string key = reader.GetString() ?? throw new JsonException("Key cannot be null");
            _ = reader.Read();

            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString() ?? throw new JsonException("Value cannot be null");
                map[key] = new PublishEntry(value);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                PublishEntry entry = ReadPublishEntry(ref reader);
                map[key] = entry;
            }
            else
            {
                throw new JsonException("Expected String or StartObject token");
            }
        }

        return map;
    }

    static PublishEntry ReadPublishEntry(ref Utf8JsonReader reader)
    {
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
                    mode = Enum.Parse<TopicRoutingMode>(reader.GetString() ?? throw new JsonException("RoutingMode cannot be null"));
                    break;
                default:
                    break;
            }
        }

        return topicName is null ? throw new JsonException("Topic is required") : new PublishEntry(topicName, mode);
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, PublishEntry> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (KeyValuePair<string, PublishEntry> pair in value)
        {
            writer.WritePropertyName(pair.Key);
            WritePublishEntry(writer, pair.Value);
        }

        writer.WriteEndObject();
    }

    static void WritePublishEntry(Utf8JsonWriter writer, PublishEntry entry)
    {
        if (entry.RoutingMode is null)
        {
            writer.WriteStringValue(entry.Topic);
        }
        else
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Topic");
            writer.WriteStringValue(entry.Topic);
            writer.WritePropertyName("RoutingMode");
            writer.WriteStringValue(entry.RoutingMode.Value.ToString());
            writer.WriteEndObject();
        }
    }
}