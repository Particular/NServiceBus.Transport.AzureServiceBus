namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using Azure.Messaging.ServiceBus;
using Configuration;

static class MessageExtensions
{
    public static Dictionary<string, string?> GetNServiceBusHeaders(this ServiceBusReceivedMessage message)
    {
        var headers = new Dictionary<string, string?>(message.ApplicationProperties.Count);

        foreach (var kvp in message.ApplicationProperties)
        {
            headers[kvp.Key] = kvp.Value?.ToString();
        }

        headers.Remove(TransportMessageHeaders.TransportEncoding);

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            headers[Headers.ReplyToAddress] = message.ReplyTo;
        }

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            headers[Headers.CorrelationId] = message.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(message.ContentType))
        {
            headers[Headers.ContentType] = message.ContentType;
        }

        return headers;
    }

    public static string GetMessageId(this ServiceBusReceivedMessage message)
    {
        if (string.IsNullOrEmpty(message.MessageId))
        {
            throw new Exception("Azure Service Bus MessageId is required, but was not found. Ensure to assign MessageId to all Service Bus messages.");
        }

        return message.MessageId;
    }

    public static BinaryData GetBody(this ServiceBusReceivedMessage message)
    {
        var body = message.Body ?? new BinaryData([]);
        var memory = body.ToMemory();

        if (memory.IsEmpty ||
            !message.ApplicationProperties.TryGetValue(TransportMessageHeaders.TransportEncoding, out var value) ||
            !value.Equals("wcf/byte-array"))
        {
            return body;
        }

        using var reader = XmlDictionaryReader.CreateBinaryReader(body.ToStream(), XmlDictionaryReaderQuotas.Max);
        var bodyBytes = (byte[])Deserializer.ReadObject(reader)!;
        return new BinaryData(bodyBytes);
    }

    static readonly DataContractSerializer Deserializer = new(typeof(byte[]));
}