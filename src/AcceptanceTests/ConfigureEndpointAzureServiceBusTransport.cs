using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var transport = configuration.UseTransport<AzureServiceBusTransport>();

        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        transport.ConnectionString(connectionString);

        transport.SubscriptionNamingConvention(name => Shorten(name));

        transport.SubscriptionRuleNamingConvention(eventType => Shorten(eventType.FullName));

        configuration.RegisterComponents(c => c.ConfigureComponent<TestIndependenceMutator>(DependencyLifecycle.SingleInstance));

        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        // w/o retries ASB will move attempted messages to the error queue right away, which will cause false failure.
        // ScenarioRunner.PerformScenarios() verifies by default no messages are moved into error queue. If it finds any, it fails the test.
        configuration.Recoverability().Immediate(retriesSettings => retriesSettings.NumberOfRetries(3));

        return Task.CompletedTask;
    }

    static string Shorten(string name)
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

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}
