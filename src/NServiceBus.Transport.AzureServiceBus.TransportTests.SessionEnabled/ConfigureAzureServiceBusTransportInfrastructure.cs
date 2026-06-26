namespace NServiceBus.Transport.AzureServiceBus.TransportTests.SessionEnabled;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.TransportTests;

public class ConfigureAzureServiceBusTransportInfrastructure : IConfigureTransportInfrastructure
{
    public static readonly string ConnectionString = Environment.GetEnvironmentVariable("AzureServiceBus_OrderedConnectionString");

    public TransportDefinition CreateTransportDefinition()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException("Environment variable AzureServiceBus_OrderedConnectionString not set");
        }

        var transport = new AzureServiceBusTransport(ConnectionString, TopicTopology.Default) { EnableSessions = true };
        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueueName, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            [
                new ReceiveSettings(inputQueueName.ToString(), inputQueueName, true, false, errorQueueName)
            ],
            [],
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}