namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    //TODO: does this need to be used by the pump?
    static class MessageReceiverExtensions
    {
        public static async Task SafeCompleteAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, ServiceBusReceivedMessage message, Transaction committableTransaction = null, CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        public static async Task SafeAbandonAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, ServiceBusReceivedMessage message, Transaction committableTransaction = null, CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        public static Task SafeDeadLetterAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, ServiceBusReceivedMessage message, string deadLetterReason, string deadLetterErrorDescription, CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                return messageReceiver.DeadLetterMessageAsync(message, deadLetterReason, deadLetterErrorDescription, cancellationToken);
            }

            return Task.CompletedTask;
        }
    }
}