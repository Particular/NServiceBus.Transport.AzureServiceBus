namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Azure.Messaging.ServiceBus;

static class OutgoingMessageExtensions
{
    // The actual property size limit is 32767 but the total size of all properties must be at most 65534.
    // Based on https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas
    // We also use some buffer to avoid hitting the limit
    const int MaxPropertySize = 32767;
    const int Buffer = 2048;
    static readonly string[] HeadersToTrim = ["NServiceBus.ExceptionInfo.StackTrace", "NServiceBus.ExceptionInfo.Message"];

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

        TrimTooLongKnownHeadersInPlace(message);

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

    static void TrimTooLongKnownHeadersInPlace(ServiceBusMessage message)
    {
        var maxUtf8Bytes = MaxPropertySize - Buffer;

        Encoder? encoder = null;
        byte[]? trimBuffer = null;

        try
        {
            foreach (var headerName in HeadersToTrim)
            {
                // Each UTF-16 code unit contributes at most three UTF-8 bytes; a valid surrogate pair contributes four bytes across two code units.
                // This cheap prescan avoids scanning and counting the bytes of values that are certainly short enough.
                if (!message.ApplicationProperties.TryGetValue(headerName, out var headerValue) ||
                    headerValue is not string value ||
                    value.Length <= maxUtf8Bytes / 3)
                {
                    continue;
                }

                trimBuffer ??= ArrayPool<byte>.Shared.Rent(maxUtf8Bytes);
                encoder ??= Encoding.UTF8.GetEncoder();

                encoder.Reset();
                encoder.Convert(value.AsSpan(), trimBuffer.AsSpan(0, maxUtf8Bytes), flush: true, out _, out int bytesUsed, out bool completed);

                if (!completed)
                {
                    message.ApplicationProperties[headerName] = Encoding.UTF8.GetString(trimBuffer, 0, bytesUsed);
                }
            }
        }
        finally
        {
            if (trimBuffer is not null)
            {
                // The buffer contains header values that can hold sensitive information (stack traces, exception messages)
                // and the shared pool hands it to unrelated future renters, so it must be cleared before returning it.
                ArrayPool<byte>.Shared.Return(trimBuffer, clearArray: true);
            }
        }
    }
}