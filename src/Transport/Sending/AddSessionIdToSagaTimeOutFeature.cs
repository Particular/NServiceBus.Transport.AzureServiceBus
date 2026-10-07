namespace NServiceBus.Transport.AzureServiceBus.Sending;

using System;
using Features;

sealed class AddSessionIdToSagaTimeOutFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        _ = context.Settings.TryGet<TransportDefinition>(out var transportDefinition);
        if (transportDefinition is not AzureServiceBusTransport azureServiceBusTransport)
        {
            throw new ArgumentException("Azure Service Bus transport must be enabled to use this feature");
        }

        if (azureServiceBusTransport.EnableSessions)
        {
            context.Pipeline.Register(
                new AddSessionIdToSagaTimeOutBehavior(),
                "Adds the session id to the saga timeout message");
        }
    }
}