using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.TransportTests;

public class ConfigureAzureServiceBusTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var result = new TransportConfigurationResult();

        result.PurgeInputQueueOnStartup = false;

        var transport = new AzureServiceBusTransport();
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        result.TransportInfrastructure = transport.Initialize(settings, connectionString);

        return result;
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}
