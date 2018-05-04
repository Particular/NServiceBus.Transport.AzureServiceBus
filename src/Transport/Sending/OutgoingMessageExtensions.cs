namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Configuration;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.InteropExtensions;
    using Performance.TimeToBeReceived;

    static class OutgoingMessageExtensions
    {
        public static Message ToAzureServiceBusMessage(this OutgoingMessage outgoingMessage, List<DeliveryConstraint> deliveryConstraints)
        {
            // TODO: Update to mimic https://github.com/Particular/NServiceBus.AzureServiceBus/blob/92a6e698deab8d73a6664e267569c860a7b6f965/src/Transport/Sending/BatchedOperationsToBrokeredMessagesConverter.cs

            var message = CreateBrokeredMessage(outgoingMessage);

            message.MessageId = Guid.NewGuid().ToString();

            ApplyDeliveryConstraints(message, deliveryConstraints);

            ApplyCorrelationId(message, outgoingMessage.Headers);

            SetReplyToAddress(message, outgoingMessage.Headers);

            CopyHeaders(message, outgoingMessage.Headers);

            // TODO: SetViaPartitionKeyToIncomingBrokeredMessagePartitionKey

            return message;
        }

        static Message CreateBrokeredMessage(OutgoingMessage outgoingMessage)
        {
            Message message = null;
            
            // TODO: get from configuration
            var configuredBodyType = TransportMessageEncoding.ByteArray;

            var serializer = DataContractBinarySerializer<byte[]>.Instance;

            switch (configuredBodyType)
            {
                case TransportMessageEncoding.ByteArray:
                    if (outgoingMessage.Body != null)
                    {
                        using (var memoryStream = new MemoryStream(outgoingMessage.Body.Length))
                        {
                            serializer.WriteObject(memoryStream, outgoingMessage.Body);

                            message = new Message(memoryStream.ToArray());
                        }
                    }
                    else
                    {
                        message = new Message();
                    }

                    message.UserProperties[TransportMessageHeaders.TransportEncoding] = "wcf/byte-array";
                    break;

                case TransportMessageEncoding.Stream:
                    message = outgoingMessage.Body != null ? new Message(outgoingMessage.Body) : new Message();

                    // TODO: typo, should be "application/octet-stream" (issue https://github.com/Particular/NServiceBus.AzureServiceBus/issues/707)
                    // For backwards compatibility, we'd need to support both and ASB Forwarding topology endpoints should understand it for interop
                    message.UserProperties[TransportMessageHeaders.TransportEncoding] = "application/octect-stream";
                    break;
            }

            return message;
        }


        static void ApplyDeliveryConstraints(Message message, List<DeliveryConstraint> deliveryConstraints)
        {
            DateTime? scheduledEnqueueTime = null;

            if (deliveryConstraints.TryGet(out DoNotDeliverBefore doNotDeliverBefore))
            {
                scheduledEnqueueTime = doNotDeliverBefore.At;
            }
            else if(deliveryConstraints.TryGet(out DelayDeliveryWith delayDeliveryWith))
            {
                scheduledEnqueueTime = Time.UtcNow() + delayDeliveryWith.Delay;
            }

            if (scheduledEnqueueTime.HasValue)
            {
                message.ScheduledEnqueueTimeUtc = scheduledEnqueueTime.Value;
            }

            if (deliveryConstraints.TryGet(out DiscardIfNotReceivedBefore discardIfNotReceivedBefore))
            {
                message.TimeToLive = discardIfNotReceivedBefore.MaxTime;
            }
        }

        static bool TryGet<T>(this List<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint => (constraint = list.OfType<T>().FirstOrDefault()) != null;

        static void ApplyCorrelationId(Message message, Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(Headers.CorrelationId))
            {
                message.CorrelationId = headers[Headers.CorrelationId];
            }
        }

        static void SetReplyToAddress(Message message, Dictionary<string, string> headers)
        {
            var replyToAddress = headers[Headers.ReplyToAddress];

            // Read-only endpoints have no reply-to value
            if (string.IsNullOrWhiteSpace(replyToAddress))
            {
                return;
            }
            
            message.ReplyTo = replyToAddress;
        }

        static void CopyHeaders(Message outgoingMessage, Dictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                outgoingMessage.UserProperties[header.Key] = header.Value;
            }
        }
    }
}