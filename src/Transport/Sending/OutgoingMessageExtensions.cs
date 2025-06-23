namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

static class OutgoingMessageExtensions
{
    public static ServiceBusMessage ToAzureServiceBusMessage(
        this IOutgoingTransportOperation outgoingTransportOperation,
        string? incomingQueuePartitionKey
    )
    {
        var outgoingMessage = outgoingTransportOperation.Message;
        var message = new ServiceBusMessage(outgoingMessage.Body)
        {
            // Cannot re-use MessageId to be compatible with ASB transport that could have native de-dup enabled
            MessageId = Guid.NewGuid().ToString(),
            TransactionPartitionKey = incomingQueuePartitionKey
        };

        ApplyDeliveryConstraints(message, outgoingTransportOperation.Properties);

        ApplyCorrelationId(message, outgoingMessage.Headers);

        ApplyContentType(message, outgoingMessage.Headers);

        SetReplyToAddress(message, outgoingMessage.Headers);

        CopyHeaders(message, outgoingMessage.Headers);

        return message;
    }

    static void ApplyDeliveryConstraints(ServiceBusMessage message, DispatchProperties dispatchProperties)
    {
        if (dispatchProperties.DoNotDeliverBefore != null)
        {
            message.ScheduledEnqueueTime = dispatchProperties.DoNotDeliverBefore.At;
        }
        else if (dispatchProperties.DelayDeliveryWith != null)
        {
            // Delaying with TimeSpan is currently not supported, see https://github.com/Azure/azure-service-bus-dotnet/issues/160
            message.ScheduledEnqueueTime = DateTimeOffset.UtcNow + dispatchProperties.DelayDeliveryWith.Delay;
        }

        if (dispatchProperties.DiscardIfNotReceivedBefore != null)
        {
            message.TimeToLive = dispatchProperties.DiscardIfNotReceivedBefore.MaxTime;
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
}