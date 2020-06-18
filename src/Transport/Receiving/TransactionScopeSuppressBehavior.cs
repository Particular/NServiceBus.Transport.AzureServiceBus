namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Pipeline;

    class TransactionScopeSuppressBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
    {
        public async Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
        {
            if (Transaction.Current != null)
            {
                using (var tx = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await next(context).ConfigureAwait(false);

                    tx.Complete();
                }
            }
            else
            {
                await next(context).ConfigureAwait(false);
            }
        }

        public class Registration : RegisterStep
        {
            public Registration() : base("HandlerTransactionScopeSuppressWrapper", typeof(TransactionScopeSuppressBehavior), "Makes sure that the handlers gets wrapped in a suppress transaction scope, preventing the ASB transaction scope from promoting")
            {
                InsertBefore("ExecuteUnitOfWork");
            }
        }
    }
}