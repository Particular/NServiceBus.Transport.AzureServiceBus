using System;
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

        configuration.RegisterComponents(c => c.ConfigureComponent<TestIndependenceMutator>(DependencyLifecycle.SingleInstance));

        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        // w/o retries ASB will move attempted messages to the error queue right away, which will cause false failure.
        // ScenarioRunner.PerformScenarios() verifies by default no messages are moved into error queue. If it finds any, it fails the test.
        configuration.Recoverability().Immediate(retriesSettings => retriesSettings.NumberOfRetries(3));

        return Task.CompletedTask;
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}
