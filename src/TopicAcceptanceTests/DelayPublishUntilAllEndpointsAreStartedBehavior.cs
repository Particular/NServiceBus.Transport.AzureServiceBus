namespace NServiceBus.Transport.AzureServiceBus.Topic.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Pipeline;

class DelayPublishUntilAllEndpointsAreStartedBehavior(ScenarioContext scenarioContext) : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
{
    public async Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
    {
        if (context.Headers.TryGetValue(Headers.MessageIntent, out var intent) && Enum.TryParse<MessageIntent>(intent, false, out var messageIntent) && messageIntent == MessageIntent.Publish)
        {
            await WaitUntilStarted(scenarioContext, context.CancellationToken);
        }

        await next(context);
    }

    static async Task WaitUntilStarted(ScenarioContext scenarioContext, CancellationToken cancellationToken)
    {
        while (!scenarioContext.EndpointsStarted)
        {
            await Task.Delay(20, cancellationToken);
        }
    }
}