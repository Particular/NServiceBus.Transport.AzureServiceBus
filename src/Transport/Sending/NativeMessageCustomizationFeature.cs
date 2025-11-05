namespace NServiceBus;

using Features;

sealed class NativeMessageCustomizationFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var isOutboxEnabled = context.Settings.IsFeatureEnabled<Features.Outbox>();
        context.Pipeline.Register(new NativeMessageCustomizationBehavior(isOutboxEnabled), "Passes native message customizations to the transport");
    }
}