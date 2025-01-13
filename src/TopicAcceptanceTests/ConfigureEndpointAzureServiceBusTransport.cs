using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing;
using NServiceBus.AcceptanceTests.Sagas;
using NServiceBus.AcceptanceTests.Versioning;
using NServiceBus.MessageMutator;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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
            topology.PublishTo(eventType, GetTopicName(eventType));
            topology.SubscribeTo(eventType, GetTopicName(eventType));
        }

        var transport = new AzureServiceBusTransport(connectionString)
        {
            Topology = topology,
            SubscriptionNamingConvention = name => Shorten(name),
        };

        ApplyMappingsToSupportMultipleInheritance(endpointName, topology);

        configuration.UseTransport(transport);

        configuration.RegisterComponents(c => c.AddSingleton<IMutateOutgoingTransportMessages, TestIndependenceMutator>());
        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    static string GetTopicName(Type eventType) => eventType.FullName.Replace("+", ".");

    static void ApplyMappingsToSupportMultipleInheritance(string endpointName, TopicPerEventTopology topology)
    {
        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
        {
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA>(
                GetTopicName(typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent)));
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB>(
                GetTopicName(typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent)));
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
        {
            topology.SubscribeTo<When_started_by_base_event_from_other_saga.IBaseEvent>(
                GetTopicName(typeof(When_started_by_base_event_from_other_saga.ISomethingHappenedEvent)));
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_multiple_versions_of_a_message_is_published.V1Subscriber)))
        {
            topology.SubscribeTo<When_multiple_versions_of_a_message_is_published.V1Event>(
                GetTopicName(typeof(When_multiple_versions_of_a_message_is_published.V2Event)));
        }
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
