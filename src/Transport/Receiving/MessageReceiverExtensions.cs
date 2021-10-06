namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    static class MessageReceiverExtensions
    {
        public static async Task SafeCompleteMessageAsync(this ServiceBusReceiver messageReceiver, ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode, Transaction committableTransaction = null)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.CompleteMessageAsync(message).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        public static async Task SafeAbandonMessageAsync(this ServiceBusReceiver messageReceiver, ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode, Transaction committableTransaction = null)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.AbandonMessageAsync(message).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }
    }
}