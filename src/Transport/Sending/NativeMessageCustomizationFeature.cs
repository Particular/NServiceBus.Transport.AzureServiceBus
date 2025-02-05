namespace NServiceBus;

using Features;

sealed class NativeMessageCustomizationFeature : Feature
{
    public NativeMessageCustomizationFeature() => EnableByDefault();

    protected override void Setup(FeatureConfigurationContext context)
    {
        var isOutboxEnabled = context.Settings.IsFeatureEnabled(typeof(Features.Outbox));
        context.Pipeline.Register(new NativeMessageCustomizationBehavior(isOutboxEnabled), "Passes native message customizations to the transport");
    }
}