using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;

public class ConfigureAzureServiceBusTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("envvar AzureServiceBus_ConnectionString not set");
        }
        var transport = new AzureServiceBusTransport(connectionString)
        {
            SubscriptionNamingConvention = name =>
            {
                // originally we used to shorten only when the length of the name has exceeded the maximum length of 50 characters
                if (name.Length <= 50)
                {
                    return name;
                }

                using (var sha1 = SHA1.Create())
                {
                    var nameAsBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(name));
                    return HexStringFromBytes(nameAsBytes);

                    string HexStringFromBytes(byte[] bytes)
                    {
                        var sb = new StringBuilder();
                        foreach (var b in bytes)
                        {
                            var hex = b.ToString("x2");
                            sb.Append(hex);
                        }

                        return sb.ToString();
                    }
                }
            }
        };
        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueueName, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            new[]
            {
                new ReceiveSettings(inputQueueName.ToString(), inputQueueName, true, false, errorQueueName),
            },
            Array.Empty<string>(),
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}