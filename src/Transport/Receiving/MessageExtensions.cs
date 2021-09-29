namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using Azure.Messaging.ServiceBus;
    using Configuration;

    static class MessageExtensions
    {
        public static Dictionary<string, string> GetNServiceBusHeaders(this ServiceBusReceivedMessage message)
        {
            var headers = new Dictionary<string, string>(message.ApplicationProperties.Count);

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

        //TODO: verify if the new SDK supports sending & receiving "wcf/byte-array"
        public static ReadOnlyMemory<byte> GetBody(this ServiceBusReceivedMessage message)
        {
            if (message.ApplicationProperties.TryGetValue(TransportMessageHeaders.TransportEncoding, out var value) && value.Equals("wcf/byte-array"))
            {
                return !message.Body.ToMemory().IsEmpty ? message.Body : ReadOnlyMemory<byte>.Empty;
            }

            return message.Body ?? ReadOnlyMemory<byte>.Empty;
        }
    }
}