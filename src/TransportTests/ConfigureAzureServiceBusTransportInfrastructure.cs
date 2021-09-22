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
        var connectionString = @"Endpoint=sb://michalw.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=i91CFUC8uX16/fsMUwblBgyw+ivhLwsb7UcXkNtI5NM=";
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

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            new[]
            {
                new ReceiveSettings(inputQueueName, inputQueueName, true, false, errorQueueName),
            },
            new string[0],
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}