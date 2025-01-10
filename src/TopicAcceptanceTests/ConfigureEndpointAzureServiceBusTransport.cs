using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        var topology = TopicTopology.Default;
        foreach (var eventType in publisherMetadata.Publishers.SelectMany(p => p.Events))
        {
            topology.PublishTo(eventType, eventType.FullName.Replace("+", "."));
            topology.SubscribeTo(eventType, eventType.FullName.Replace("+", "."));
        }

        var transport = new AzureServiceBusTransport(connectionString)
        {
            Topology = topology,
            SubscriptionNamingConvention = name => Shorten(name),
        };

        configuration.UseTransport(transport);

        configuration.RegisterComponents(c => c.AddSingleton<IMutateOutgoingTransportMessages, TestIndependenceMutator>());
        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    static string Shorten(string name)
    {
        // originally we used to shorten only when the length of the name hax exceeded the maximum length of 50 characters
        if (name.Length <= 50)
        {
            return name;
        }

        using var sha1 = SHA1.Create();
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

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}
