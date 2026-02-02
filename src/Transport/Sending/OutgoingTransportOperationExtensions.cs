namespace NServiceBus.Transport.AzureServiceBus;

using System;
using Azure.Messaging.ServiceBus;
using EventRouting;
using Logging;

static class OutgoingTransportOperationExtensions
{
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
        DestinationManager destinationManager)
    {
        string destination;
        string? enclosedMessageTypes;

        switch (outgoingTransportOperation)
        {
            case MulticastTransportOperation multicastTransportOperation:
                destination = topology.GetPublishDestination(multicastTransportOperation.MessageType);
                _ = multicastTransportOperation.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes);
                break;

            case UnicastTransportOperation unicastTransportOperation:
                destination = unicastTransportOperation.Destination;
                _ = unicastTransportOperation.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes);

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

        return destinationManager.GetDestination(destination, enclosedMessageTypes);
    }
}