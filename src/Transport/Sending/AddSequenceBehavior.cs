namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Threading.Tasks;
using Pipeline;

sealed class AddSequenceBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
{
    public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
    {
        if (context.Extensions.TryGet(out SessionOrderingProvider? orderingProvider)
            && context.Extensions.TryGet(out DispatchProperties? dispatchProperties))
        {
            dispatchProperties["Sequence"] = orderingProvider.NextSequence();
        }

        return next(context);
    }
}