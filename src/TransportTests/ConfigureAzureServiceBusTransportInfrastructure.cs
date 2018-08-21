using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.TransportTests;

public class ConfigureAzureServiceBusTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        // TODO: remove when Core 7.1 is out
        settings.Set<StartupDiagnosticEntries>(new StartupDiagnosticEntries());

        var result = new TransportConfigurationResult
        {
            PurgeInputQueueOnStartup = false
        };

        var transport = new AzureServiceBusTransport();

        var transportExtensions = new TransportExtensions<AzureServiceBusTransport>(settings);

        transportExtensions.SubscriptionNameShortener(name =>
        {
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
        });

        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        result.TransportInfrastructure = transport.Initialize(settings, connectionString);

        return result;
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}
