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
        CreateStartupDiagnostics(settings);

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

    static void CreateStartupDiagnostics(SettingsHolder settings)
    {
        var ctor = hostingSettingsType.GetConstructors()[0];
        var hostingSettings = ctor.Invoke(new object[] {settings});
        settings.Set(hostingSettingsType.FullName, hostingSettings);
    }

    static Type hostingSettingsType = typeof(IEndpointInstance).Assembly.GetType("NServiceBus.HostingComponent+Settings", true);
}