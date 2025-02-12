namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class SubscribedEventToTopicsMapConverter : JsonConverter<Dictionary<string, HashSet<string>>>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(Dictionary<string, HashSet<string>>).IsAssignableFrom(typeToConvert);

    public override Dictionary<string, HashSet<string>> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var map = new Dictionary<string, HashSet<string>>();

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
                map[key] = [value];
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                var set = new HashSet<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        _ = set.Add(reader.GetString() ?? throw new JsonException("Value cannot be null"));
                    }
                    else
                    {
                        throw new JsonException("Expected String token");
                    }
                }

                map[key] = set;
            }
            else
            {
                throw new JsonException("Expected String or StartArray token");
            }
        }

        return map;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, HashSet<string>> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (KeyValuePair<string, HashSet<string>> pair in value)
        {
            writer.WritePropertyName(pair.Key);

            if (pair.Value == null)
            {
                throw new JsonException("Value cannot be null");
            }

            if (pair.Value.Count == 1)
            {
                writer.WriteStringValue(pair.Value.ElementAt(0));
            }
            else
            {
                writer.WriteStartArray();
                foreach (string item in pair.Value)
                {
                    writer.WriteStringValue(item);
                }

                writer.WriteEndArray();
            }
        }

        writer.WriteEndObject();
    }
}