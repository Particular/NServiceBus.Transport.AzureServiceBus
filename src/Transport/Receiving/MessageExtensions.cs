namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using Azure.Messaging.ServiceBus;
using Configuration;
using Faults;

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

        if (!headers.ContainsKey(FaultsHeaderKeys.FailedQ) && !string.IsNullOrWhiteSpace(message.DeadLetterSource))
        {
            headers[FaultsHeaderKeys.FailedQ] = message.DeadLetterSource;
        }

        if (!headers.ContainsKey(FaultsHeaderKeys.Message) && !string.IsNullOrWhiteSpace(message.DeadLetterReason))
        {
            headers[FaultsHeaderKeys.Message] = message.DeadLetterReason;
        }

        if (!headers.ContainsKey(FaultsHeaderKeys.StackTrace) && !string.IsNullOrWhiteSpace(message.DeadLetterErrorDescription))
        {
            headers[FaultsHeaderKeys.StackTrace] = message.DeadLetterErrorDescription;
        }

        return headers;
    }

    public static string GetMessageId(this ServiceBusReceivedMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.MessageId))
        {
            return message.MessageId;
        }

        if (message.ApplicationProperties.TryGetValue(Headers.MessageId, out var id) && id?.ToString() is { } messageId && !string.IsNullOrWhiteSpace(messageId))
        {
            return messageId;
        }

        return GuidHelper.CreateVersion8(message.EnqueuedTime, message.SequenceNumber).ToString();
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