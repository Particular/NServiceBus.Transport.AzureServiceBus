namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class SubscribedEventToTopicsMapConverter : JsonConverter<Dictionary<string, HashSet<SubscriptionEntry>>>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(Dictionary<string, HashSet<SubscriptionEntry>>) ||
        typeToConvert == typeof(Dictionary<string, HashSet<string>>);

    public override Dictionary<string, HashSet<SubscriptionEntry>> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var map = new Dictionary<string, HashSet<SubscriptionEntry>>();

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
                map[key] = [new SubscriptionEntry(value, SubscriptionFilterMode.Default)];
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                var set = new HashSet<SubscriptionEntry>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        string value = reader.GetString() ?? throw new JsonException("Value cannot be null");
                        _ = set.Add(new SubscriptionEntry(value, SubscriptionFilterMode.Default));
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        SubscriptionEntry entry = ReadSubscriptionEntry(ref reader);
                        _ = set.Add(entry);
                    }
                    else
                    {
                        throw new JsonException("Expected String or StartObject token");
                    }
                }

                map[key] = set;
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                SubscriptionEntry entry = ReadSubscriptionEntry(ref reader);
                map[key] = [entry];
            }
            else
            {
                throw new JsonException("Expected String, StartArray, or StartObject token");
            }
        }

        return map;
    }

    SubscriptionEntry ReadSubscriptionEntry(ref Utf8JsonReader reader)
    {
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

    public override void Write(Utf8JsonWriter writer, Dictionary<string, HashSet<SubscriptionEntry>> value,
        JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (KeyValuePair<string, HashSet<SubscriptionEntry>> pair in value)
        {
            writer.WritePropertyName(pair.Key);

            if (pair.Value == null)
            {
                throw new JsonException("Value cannot be null");
            }

            if (pair.Value.Count == 1)
            {
                var entry = pair.Value.GetEnumerator();
                entry.MoveNext();
                WriteSubscriptionEntry(writer, entry.Current);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var entry in pair.Value)
                {
                    WriteSubscriptionEntry(writer, entry);
                }
                writer.WriteEndArray();
            }
        }

        writer.WriteEndObject();
    }

    void WriteSubscriptionEntry(Utf8JsonWriter writer, SubscriptionEntry entry)
    {
        if (entry.FilterMode == SubscriptionFilterMode.Default)
        {
            writer.WriteStringValue(entry.Topic);
        }
        else
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Topic");
            writer.WriteStringValue(entry.Topic);
            writer.WritePropertyName("FilterMode");
            writer.WriteStringValue(entry.FilterMode.ToString());
            writer.WriteEndObject();
        }
    }
}
