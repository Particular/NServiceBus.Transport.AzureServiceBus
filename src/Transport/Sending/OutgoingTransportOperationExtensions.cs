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

    public static (string Destination, string? EnclosedMessageTypes, TopicRoutingMode RoutingMode) ExtractDestinationAndMultiplexingOptions(
        this IOutgoingTransportOperation outgoingTransportOperation,
        TopicTopology topology,
        DestinationManager destinationManager)
    {
        string destination;
        string? enclosedMessageTypes;
        TopicRoutingMode routingMode;

        switch (outgoingTransportOperation)
        {
            case MulticastTransportOperation multicastTransportOperation:
                destination = topology.GetPublishDestination(multicastTransportOperation.MessageType);
                var eventTypeFullName = multicastTransportOperation.MessageType.FullName;
                _ = multicastTransportOperation.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes);
                enclosedMessageTypes ??= eventTypeFullName;
                routingMode = eventTypeFullName != null ? topology.GetTopicRoutingMode(eventTypeFullName) : TopicRoutingMode.NotMultiplexed;
                break;

            case UnicastTransportOperation unicastTransportOperation:
                destination = unicastTransportOperation.Destination;
                _ = unicastTransportOperation.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes);

                var index = unicastTransportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination[..index];
                }

                routingMode = TopicRoutingMode.NotMultiplexed;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
        }

        var resolvedDestination = destinationManager.GetDestination(destination, enclosedMessageTypes);
        return (resolvedDestination, enclosedMessageTypes, routingMode);
    }

    public static string ExtractDestination(this IOutgoingTransportOperation outgoingTransportOperation,
        TopicTopology topology,
        DestinationManager destinationManager)
    {
        var (destination, _, _) = outgoingTransportOperation.ExtractDestinationAndMultiplexingOptions(topology, destinationManager);
        return destination;
    }
}
