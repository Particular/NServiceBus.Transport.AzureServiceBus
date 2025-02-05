namespace NServiceBus.Transport.AzureServiceBus;

using System.Transactions;

static class TransactionExtensions
{
    public static TransactionScope ToScope(this Transaction? transaction) =>
        transaction != null
            ? new TransactionScope(transaction, TransactionScopeAsyncFlowOption.Enabled)
            : new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
}