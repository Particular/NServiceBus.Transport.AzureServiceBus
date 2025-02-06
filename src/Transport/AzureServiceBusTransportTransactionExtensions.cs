namespace NServiceBus.Transport.AzureServiceBus;

using System.Transactions;

/// <summary>
/// Provides extension methods for <see cref="AzureServiceBusTransportTransaction"/>
/// </summary>
public static class AzureServiceBusTransportTransactionExtensions
{
    /// <summary>
    /// Returns a new scope that takes into account the internally managed transaction by either
    /// providing the transaction to the scope or creating a suppress scope.
    /// </summary>
    public static TransactionScope ToTransactionScope(
        this AzureServiceBusTransportTransaction azureServiceBusTransaction) =>
        azureServiceBusTransaction.Transaction.ToScope();
}