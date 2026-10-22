namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Threading.Tasks;
using Pipeline;

sealed class AddSequenceBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
{
    public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
    {
        //HINT: We are only interested in ordering messages with session ID
        if (context.Extensions.TryGet(out SessionOrderingProvider? orderingProvider)
            && context.Extensions.TryGet(out DispatchProperties? dispatchProperties)
            && dispatchProperties.ContainsKey("SessionId"))
        {
            dispatchProperties["NServiceBus.Transport.AzureServiceBus.Sequence"] = orderingProvider.NextSequence();
        }

        return next(context);
    }
}