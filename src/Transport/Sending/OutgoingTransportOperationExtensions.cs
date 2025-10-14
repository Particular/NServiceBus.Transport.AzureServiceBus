namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Logging;

static class OutgoingTransportOperationExtensions
{
    static readonly ConcurrentDictionary<string, string> destinationCache = [];

    public static void ApplyCustomizationToOutgoingNativeMessage(
        this IOutgoingTransportOperation transportOperation,
        ServiceBusMessage message, TransportTransaction transportTransaction, ILog logger)
    {
        if (!transportOperation.Properties.TryGetValue(NativeMessageCustomizationBehavior.CustomizationKey,
                out var key))
        {
            return;
        }

        var messageCustomizer = transportTransaction.Get<NativeMessageCustomizer>();
        if (!messageCustomizer.Customizations.TryGetValue(key, out var action))
        {
            logger.Warn(
                $"Message {transportOperation.Message.MessageId} was configured with a native message customization but the customization was not found in {nameof(NativeMessageCustomizer)}");
            return;
        }

        action(message);
    }

    public static string ExtractDestination(this IOutgoingTransportOperation outgoingTransportOperation,
        TopicTopology topology,
        string? hierarchyNamespace)
    {
        string destination;

        switch (outgoingTransportOperation)
        {
            case MulticastTransportOperation multicastTransportOperation:
                destination = topology.GetPublishDestination(multicastTransportOperation.MessageType);
                break;

            case UnicastTransportOperation unicastTransportOperation:
                destination = unicastTransportOperation.Destination;

                // Workaround for reply-to address set by ASB transport
                var index = unicastTransportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination[..index];
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
        }

        return destinationCache.GetOrAdd(
            destination,
            static (dest, ns) => ns is null || dest.StartsWith(ns)
                ? dest
                : $"{ns}/{dest}", hierarchyNamespace);
    }
}