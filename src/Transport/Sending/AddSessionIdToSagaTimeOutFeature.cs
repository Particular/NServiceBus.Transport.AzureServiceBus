namespace NServiceBus.Transport.AzureServiceBus.Sending;

using Features;

sealed class AddSessionIdToSagaTimeOutFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        if (context.Settings.TryGet<TransportDefinition>(out var transportDefinition) &&
            transportDefinition is AzureServiceBusTransport azureServiceBusTransport &&
            azureServiceBusTransport.EnableSessions)
        {
            context.Pipeline.Register(
                new AddSessionIdToSagaTimeOutBehavior(),
                "Adds the session id to the saga timeout message");
        }
    }
}