using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing;
using NServiceBus.AcceptanceTests.Routing.NativePublishSubscribe;
using NServiceBus.AcceptanceTests.Sagas;
using NServiceBus.AcceptanceTests.Versioning;
using NServiceBus.MessageMutator;
using NServiceBus.Transport.AzureServiceBus;
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
        topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());

        foreach (var eventType in publisherMetadata.Publishers.SelectMany(p => p.Events))
        {
            topology.PublishTo(eventType, eventType.ToTopicName());
            topology.SubscribeTo(eventType, eventType.ToTopicName());
        }

        var transport = new AzureServiceBusTransport(connectionString, topology);

        ApplyMappingsToSupportMultipleInheritance(endpointName, topology);

        configuration.UseTransport(transport);

        configuration.RegisterComponents(c => c.AddSingleton<IMutateOutgoingTransportMessages, TestIndependenceMutator>());
        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    static void ApplyMappingsToSupportMultipleInheritance(string endpointName, TopicPerEventTopology topology)
    {
        if (endpointName == Conventions.EndpointNamingConvention(typeof(MultiSubscribeToPolymorphicEvent.Subscriber)))
        {
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent1).ToTopicName());
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent2).ToTopicName());
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.GeneralSubscriber)))
        {
            topology.SubscribeTo<When_subscribing_to_a_base_event.IBaseEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
        {
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
        {
            topology.SubscribeTo<When_started_by_base_event_from_other_saga.IBaseEvent>(
                typeof(When_started_by_base_event_from_other_saga.ISomethingHappenedEvent).ToTopicName());
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_multiple_versions_of_a_message_is_published.V1Subscriber)))
        {
            topology.SubscribeTo<When_multiple_versions_of_a_message_is_published.V1Event>(
                typeof(When_multiple_versions_of_a_message_is_published.V2Event).ToTopicName());
        }
    }

    public Task Cleanup() => Task.CompletedTask;
}
