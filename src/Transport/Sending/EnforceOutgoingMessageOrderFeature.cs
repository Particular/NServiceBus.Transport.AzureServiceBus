namespace NServiceBus.Transport.AzureServiceBus.Sending;

using Features;

sealed class EnforceOutgoingMessageOrderFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Pipeline.Register(new AddSequenceBehavior(), "Adds sequence numbers to outgoing messages");
        context.Pipeline.Register(new SessionOrderingBehavior(), "Registers the ordering provider");
    }
}