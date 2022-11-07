namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    static class ProcessMessageEventArgsExtensions
    {
        public static async Task SafeCompleteMessageAsync(this ProcessMessageEventArgs args,
            ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode,
            AzureServiceBusTransportTransaction azureServiceBusTransaction,
            CancellationToken cancellationToken = default)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using var scope = azureServiceBusTransaction.ToTransactionScope();
                await args.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);

                scope.Complete();
            }
        }

        public static Task SafeAbandonMessageAsync(this ProcessMessageEventArgs args, ServiceBusReceivedMessage message,
            TransportTransactionMode transportTransactionMode, CancellationToken cancellationToken = default)
            => transportTransactionMode != TransportTransactionMode.None
                ? args.AbandonMessageAsync(message, cancellationToken: cancellationToken)
                : Task.CompletedTask;
    }
}