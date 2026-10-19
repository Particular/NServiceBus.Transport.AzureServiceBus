namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

static class ReceivedMessageExtensions
{
    /// <summary>
    /// Builds the receiver properties object based on native properties of the SDK message.
    /// </summary>
    internal static ReceiveProperties GetReceiveProperties(this ServiceBusReceivedMessage message)
    {
        if (message.SessionId != null)
        {
            var properties = new Dictionary<string, string> { ["SessionId"] = message.SessionId };
            return new ReceiveProperties(properties);
        }

        return ReceiveProperties.Empty;
    }
}