namespace NServiceBus.Transport.AzureServiceBus;

using System;
using Azure.Messaging.ServiceBus;
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
        TopicTopology topology)
    {
        switch (outgoingTransportOperation)
        {
            case MulticastTransportOperation multicastTransportOperation:
                return topology.GetPublishDestination(multicastTransportOperation.MessageType);
            case UnicastTransportOperation unicastTransportOperation:
                var destination = unicastTransportOperation.Destination;

                // Workaround for reply-to address set by ASB transport
                var index = unicastTransportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination[..index];
                }

                return destination;
            default:
                throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
        }
    }
}