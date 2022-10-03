namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;

    static class ProcessMessageEventArgsExtensions
    {
        public static async Task SafeCompleteMessageAsync(this ProcessMessageEventArgs args, ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode, Transaction committableTransaction = null, CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using var scope = committableTransaction.ToScope();
                await args.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);

                scope.Complete();
            }
        }

        public static async Task SafeAbandonMessageAsync(this ProcessMessageEventArgs args, ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode, Transaction committableTransaction = null, CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using var scope = committableTransaction.ToScope();
                await args.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);

                scope.Complete();
            }
        }
    }
}