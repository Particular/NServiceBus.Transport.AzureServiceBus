using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.MessageMutator;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("envvar AzureServiceBus_ConnectionString not set");
        }

        var topology = TopicTopology.MigrateFromSingleDefaultTopic();
        topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());

        foreach (var eventType in publisherMetadata.Publishers.SelectMany(p => p.Events))
        {
            topology.MapToDefaultTopic(eventType);
            topology.OverrideRuleNameFor(eventType, eventType.FullName.Shorten());
        }

        var transport = new AzureServiceBusTransport(connectionString, topology);

        configuration.UseTransport(transport);

        configuration.RegisterComponents(c => c.AddSingleton<IMutateOutgoingTransportMessages, TestIndependenceMutator>());
        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;
}
