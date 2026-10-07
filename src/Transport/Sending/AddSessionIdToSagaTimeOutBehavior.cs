namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pipeline;

sealed class AddSessionIdToSagaTimeOutBehavior : Behavior<IOutgoingSendContext>
{
    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        if (context.Headers.TryGetValue(Headers.IsSagaTimeoutMessage, out var isSagaTimeoutMessage) &&
            bool.TryParse(isSagaTimeoutMessage, out var isSagaTimeout) &&
            isSagaTimeout &&
            context.Extensions.Get<IncomingMessage>() is { ReceiveProperties: not null } incomingMessage)
        {
            var sessionId = incomingMessage.ReceiveProperties.GetValueOrDefault("SessionId");

            if (!string.IsNullOrWhiteSpace(sessionId) && context.Extensions.Get<DispatchProperties>() is { } dispatchProperties)
            {
                dispatchProperties.TryAdd("SessionId", sessionId);
            }
        }
        return next();
    }
}