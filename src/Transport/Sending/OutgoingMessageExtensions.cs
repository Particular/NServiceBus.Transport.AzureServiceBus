namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using DeliveryConstraints;
    using Microsoft.Azure.ServiceBus;

    static class OutgoingMessageExtensions
    {
        public static Message ToAzureServiceBusMessage(this OutgoingMessage outgoingMessage, List<DeliveryConstraint> deliveryConstraints)
        {
            // TODO: Update to mimic https://github.com/Particular/NServiceBus.AzureServiceBus/blob/92a6e698deab8d73a6664e267569c860a7b6f965/src/Transport/Sending/BatchedOperationsToBrokeredMessagesConverter.cs

            var message = new Message(outgoingMessage.Body);

            foreach (var key in outgoingMessage.Headers.Keys)
            {
                message.UserProperties[key] = outgoingMessage.Headers[key];
            }

            return message;
        }
    }
}