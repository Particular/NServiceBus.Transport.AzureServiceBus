namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Threading;
using System.Threading.Tasks;
using Pipeline;

sealed class AddSequenceBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
{
    public const string SequenceDispatchProperty = "ASBS.Sequence";
    ulong sequence;

    public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
    {
        //HINT: We are only interested in ordering messages with session ID
        if (context.Extensions.TryGet(out DispatchProperties? dispatchProperties)
            && dispatchProperties.ContainsKey("SessionId"))
        {
            var nextSeq = Interlocked.Increment(ref sequence);

            dispatchProperties[SequenceDispatchProperty] = nextSeq.ToString();
        }

        return next(context);
    }
}