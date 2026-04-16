namespace NServiceBus;

using System;
using System.Threading.Tasks;
using ConsistencyGuarantees;
using Features;
using Pipeline;
using Transport.AzureServiceBus;

class FaultsDeadLetteringFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        if (context.Settings.GetRequiredTransactionModeForReceives() == TransportTransactionMode.None)
        {
            throw new InvalidOperationException("Dead lettering of failed messages is not valid for transport transaction mode None since any processing failure leads to the message being discarded.");
        }

        context.Pipeline.Register(typeof(NativeDlqBehavior), "Requests native service bus dead lettering of faults");
    }

    class NativeDlqBehavior : Behavior<IRecoverabilityContext>
    {
        public override Task Invoke(IRecoverabilityContext context, Func<Task> next)
        {
            if (context.RecoverabilityAction is MoveToError)
            {
                context.RecoverabilityAction = RecoverabilityAction.DeadLetter();
            }

            return next();
        }
    }
}