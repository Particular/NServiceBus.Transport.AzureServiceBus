namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using Configuration;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.InteropExtensions;

    static class MessageExtensions
    {
        public static Dictionary<string, string> GetRequiredHeaders(this Message message)
        {
            var headers = new Dictionary<string, string>(message.UserProperties.Count);

            foreach (var kvp in message.UserProperties)
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

            headers["ASB.System.LockedUntilUtc"] = message.SystemProperties.LockedUntilUtc.ToString(dateTimeFormat);

            return headers;
        }

        public static string GetMessageId(this Message message)
        {
            if (string.IsNullOrEmpty(message.MessageId))
            {
                throw new Exception("Azure Service Bus MessageId is required, but was not found. Ensure to assign MessageId to all Service Bus messages.");
            }

            return message.MessageId;
        }

        public static byte[] GetBody(this Message message)
        {
            if (message.UserProperties.TryGetValue(TransportMessageHeaders.TransportEncoding, out var value) && value.Equals("wcf/byte-array"))
            {
                return message.GetBody<byte[]>() ?? Array.Empty<byte>();
            }

            return message.Body ?? Array.Empty<byte>();
        }

        const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";
    }
}