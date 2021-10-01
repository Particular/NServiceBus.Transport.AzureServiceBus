namespace NServiceBus.Transport.AzureServiceBus
{
    //using System.Threading.Tasks;
    //using System.Transactions;
    //using Azure.Messaging.ServiceBus;

    static class MessageReceiverExtensions
    {
        //TODO: does this need to be used by the pump?
        //public static async Task SafeCompleteAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, Transaction committableTransaction = null)
        //{
        //    if (transportTransactionMode != TransportTransactionMode.None)
        //    {
        //        using (var scope = committableTransaction.ToScope())
        //        {
        //            await messageReceiver.CompleteAsync(lockToken).ConfigureAwait(false);

        //            scope.Complete();
        //        }
        //    }
        //}

        //public static async Task SafeAbandonAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, Transaction committableTransaction = null)
        //{
        //    if (transportTransactionMode != TransportTransactionMode.None)
        //    {
        //        using (var scope = committableTransaction.ToScope())
        //        {
        //            await messageReceiver.AbandonAsync(lockToken).ConfigureAwait(false);

        //            scope.Complete();
        //        }
        //    }
        //}
    }
}