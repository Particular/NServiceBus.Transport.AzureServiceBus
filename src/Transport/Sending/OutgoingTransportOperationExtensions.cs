#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
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
    }
}