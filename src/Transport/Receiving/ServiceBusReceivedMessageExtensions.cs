namespace NServiceBus.Transport.AzureServiceBus.AdvancedExtensibility;

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using Azure.Messaging.ServiceBus;
using Configuration;
using Faults;

/// <summary>
/// Helpers to get ID, headers and body for the message being processed. Intentionally public to enable reuse in the functions package.
/// </summary>
public static class ServiceBusReceivedMessageExtensions
{
    extension(ServiceBusReceivedMessage message)
    {
        /// <summary>
        /// Extracts NServiceBus headers from the <see cref="ServiceBusReceivedMessage"/>
        /// </summary>
        /// <remarks>
        /// In addition to converting all application properties to headers native message properties like ReplyTo, ContentType are also exposed as properties.
        /// For dead lettered messages source, reason and description is mapped to relevant NServiceBus fault headers for better integration with the platform.
        /// </remarks>
        public Dictionary<string, string?> GetNServiceBusHeaders()
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
                headers[FaultsHeaderKeys.ExceptionType] = message.DeadLetterReason;
            }

            if (!headers.ContainsKey(FaultsHeaderKeys.StackTrace) && !string.IsNullOrWhiteSpace(message.DeadLetterErrorDescription))
            {
                headers[FaultsHeaderKeys.Message] = message.DeadLetterErrorDescription;
            }

            return headers;
        }

        /// <summary>
        /// Returns the message ID for the <see cref="ServiceBusReceivedMessage"/>.
        /// </summary>
        /// <remarks>
        /// If no native message ID is present (testing indicates that the service always assigns one) the NServiceBus message ID will be used.
        /// If none of these are present a deterministic GUID based in the EnqueuedTime and SequenceNumber is used to ensure an ID is always returned.
        /// </remarks>
        public string GetMessageId()
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

        /// <summary>
        /// Returns the message body of the <see cref="ServiceBusReceivedMessage"/>.
        /// </summary>
        /// <remarks>
        /// If the NServiceBus.Transport.Encoding application property indicates that the message originates from a legacy endpoint, (wcf/byte-array),
        /// the body is deserialized using a XmlDictionaryReader for backwards compatibility.
        /// </remarks>
        public BinaryData GetBody()
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
    }

    static readonly DataContractSerializer Deserializer = new(typeof(byte[]));
}