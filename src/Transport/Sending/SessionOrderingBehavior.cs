namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Threading;
using System.Threading.Tasks;
using Pipeline;

sealed class SessionOrderingBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
{
    public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
    {
        var orderingProvider = new SessionOrderingProvider();
        context.Extensions.Set(orderingProvider);

        return next(context);
    }
}

class SessionOrderingProvider
{
    int sequence;

    public string NextSequence()
    {
        var next = Interlocked.Increment(ref sequence);
        return next.ToString();
    }
}