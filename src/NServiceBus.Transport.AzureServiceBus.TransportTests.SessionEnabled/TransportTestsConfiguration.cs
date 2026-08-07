namespace NServiceBus.TransportTests;

using Transport.AzureServiceBus.TransportTests.SessionEnabled;

public partial class TransportTestsConfiguration
{
    public IConfigureTransportInfrastructure CreateTransportConfiguration() => new ConfigureAzureServiceBusTransportInfrastructure();
}