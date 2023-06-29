namespace NServiceBus.Transport.AzureServiceBus
{
    using System;

    static class OutgoingTransportOperationExtensions
    {
        public static string ExtractDestination(this IOutgoingTransportOperation outgoingTransportOperation, string defaultMulticastRoute)
        {
            switch (outgoingTransportOperation)
            {
                case MulticastTransportOperation _:
                    return defaultMulticastRoute;
                case UnicastTransportOperation unicastTransportOperation:
                    var destination = unicastTransportOperation.Destination;

                    // Workaround for reply-to address set by ASB transport
                    var index = unicastTransportOperation.Destination.IndexOf('@');

                    if (index > 0)
                    {
                        destination = destination.Substring(0, index);
                    }
                    return destination;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outgoingTransportOperation));
            }
        }
    }
}