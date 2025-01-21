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
    public static readonly string ConnectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

    public TransportDefinition CreateTransportDefinition()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException("envvar AzureServiceBus_ConnectionString not set");
        }
        var transport = new AzureServiceBusTransport(ConnectionString)
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
            [
                new ReceiveSettings(inputQueueName.ToString(), inputQueueName, true, false, errorQueueName)
            ],
            [],
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}