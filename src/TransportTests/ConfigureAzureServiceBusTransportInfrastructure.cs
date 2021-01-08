using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;

public class ConfigureAzureServiceBusTransportInfrastructure : IConfigureTransportInfrastructure
{
    public async Task<TransportConfigurationResult> Configure(HostSettings hostSettings, string inputQueueName, string errorQueueName,
        TransportTransactionMode transactionMode)
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        var transport = new AzureServiceBusTransport(connectionString);

        transport.TransportTransactionMode = transactionMode;
        transport.SubscriptionNamingConvention = name =>
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
        };

        var transportInfrastructure = await transport.Initialize(
            hostSettings, 
            new[]
            {
                new ReceiveSettings(inputQueueName, inputQueueName, true, false, errorQueueName), 
            }, 
            new string[0]);

        return new TransportConfigurationResult
        {
            PurgeInputQueueOnStartup = false,
            TransportDefinition = transport,
            TransportInfrastructure = transportInfrastructure,
            PushRuntimeSettings = null //TODO
        };
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}