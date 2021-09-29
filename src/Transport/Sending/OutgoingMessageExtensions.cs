namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Messaging.ServiceBus;
    using Configuration;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;

    static class OutgoingMessageExtensions
    {
        public static ServiceBusMessage ToAzureServiceBusMessage(this OutgoingMessage outgoingMessage, List<DeliveryConstraint> deliveryConstraints, string incomingQueuePartitionKey)
        {
            var message = new ServiceBusMessage(outgoingMessage.Body)
            {
                // Cannot re-use MessageId to be compatible with ASB transport that could have native de-dup enabled
                MessageId = Guid.NewGuid().ToString()
            };

            // The value needs to be "application/octect-stream" and not "application/octet-stream" for interop with ASB transport
            message.ApplicationProperties[TransportMessageHeaders.TransportEncoding] = "application/octect-stream";

            message.TransactionPartitionKey = incomingQueuePartitionKey;

            ApplyDeliveryConstraints(message, deliveryConstraints);

            ApplyCorrelationId(message, outgoingMessage.Headers);

            ApplyContentType(message, outgoingMessage.Headers);

            SetReplyToAddress(message, outgoingMessage.Headers);

            CopyHeaders(message, outgoingMessage.Headers);

            return message;
        }

        static void ApplyDeliveryConstraints(ServiceBusMessage message, List<DeliveryConstraint> deliveryConstraints)
        {
            // TODO: review when delaying with TimeSpan is supported https://github.com/Azure/azure-service-bus-dotnet/issues/160

            if (deliveryConstraints.TryGet(out DoNotDeliverBefore doNotDeliverBefore))
            {
                //TODO: datetime -> datetimeOffset
                message.ScheduledEnqueueTime = doNotDeliverBefore.At;
            }
            else if (deliveryConstraints.TryGet(out DelayDeliveryWith delayDeliveryWith))
            {
                //TODO: datetime -> datetimeOffset
                message.ScheduledEnqueueTime = Time.UtcNow() + delayDeliveryWith.Delay;
            }

            if (deliveryConstraints.TryGet(out DiscardIfNotReceivedBefore discardIfNotReceivedBefore))
            {
                message.TimeToLive = discardIfNotReceivedBefore.MaxTime;
            }
        }

        static void ApplyCorrelationId(ServiceBusMessage message, Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(Headers.CorrelationId, out var correlationId))
            {
                message.CorrelationId = correlationId;
            }
        }

        static void ApplyContentType(ServiceBusMessage message, Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(Headers.ContentType, out var contentType))
            {
                message.ContentType = contentType;
            }
        }

        static void SetReplyToAddress(ServiceBusMessage message, Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
            {
                message.ReplyTo = replyToAddress;
            }
        }

        static void CopyHeaders(ServiceBusMessage outgoingMessage, Dictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                outgoingMessage.ApplicationProperties[header.Key] = header.Value;
            }
        }

        static bool TryGet<T>(this List<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint => (constraint = list.OfType<T>().FirstOrDefault()) != null;
    }
}
