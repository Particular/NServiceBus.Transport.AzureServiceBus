namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.Azure.ServiceBus.Core;

    static class MessageReceiverExtensions
    {
        public static async Task SafeCompleteAsync(this MessageReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, Transaction committableTransaction = null)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.CompleteAsync(lockToken).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        public static async Task SafeAbandonAsync(this MessageReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, Transaction committableTransaction = null)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                using (var scope = committableTransaction.ToScope())
                {
                    await messageReceiver.AbandonAsync(lockToken).ConfigureAwait(false);

                    scope.Complete();
                }
            }
        }

        public static Task SafeDeadLetterAsync(this MessageReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, string deadLetterReason, string deadLetterErrorDescription)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                return messageReceiver.DeadLetterAsync(lockToken, deadLetterReason, deadLetterErrorDescription);
            }

            return Task.CompletedTask;
        }
    }
}