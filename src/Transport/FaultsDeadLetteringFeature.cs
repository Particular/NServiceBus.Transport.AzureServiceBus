namespace NServiceBus;

using System;
using System.Threading.Tasks;
using Features;
using Pipeline;
using Transport;
using Transport.AzureServiceBus;

class FaultsDeadLetteringFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transport = context.Settings.Get<TransportDefinition>();

        if (transport is AzureServiceBusTransport { DeadLetterFailedMessages: false })
        {
            return;
        }

        if (transport.TransportTransactionMode == TransportTransactionMode.None)
        {
            throw new ArgumentException("Dead letter failed messages is not valid for transport transaction mode None since failures leads to message being discarded.");
        }

        context.Pipeline.Register(typeof(NativeDlqBehavior), "Requests native dead lettering of faults");
    }

    class NativeDlqBehavior : Behavior<IRecoverabilityContext>
    {
        public override Task Invoke(IRecoverabilityContext context, Func<Task> next)
        {
            if (context.RecoverabilityAction.GetType() == typeof(MoveToError))
            {
                context.RecoverabilityAction = RecoverabilityAction.DeadLetter();
            }

            return next();
        }
    }
}