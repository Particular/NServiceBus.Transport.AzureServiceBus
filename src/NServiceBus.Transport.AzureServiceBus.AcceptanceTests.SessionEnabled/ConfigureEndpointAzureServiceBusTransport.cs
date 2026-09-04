using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing;
using NServiceBus.AcceptanceTests.Routing.NativePublishSubscribe;
using NServiceBus.AcceptanceTests.Sagas;
using NServiceBus.AcceptanceTests.Versioning;
using NServiceBus.Pipeline;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class GenerateRandomSessionIdForSends : Behavior<IOutgoingSendContext>
{
    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        var dispatchProperties = context.Extensions.Get<DispatchProperties>();
        dispatchProperties.TryAdd("SessionId", Guid.NewGuid().ToString());

        return next();
    }
}

public class GenerateRandomSessionIdForPublishes : Behavior<IOutgoingPublishContext>
{
    public override Task Invoke(IOutgoingPublishContext context, Func<Task> next)
    {
        var dispatchProperties = context.Extensions.Get<DispatchProperties>();
        dispatchProperties["SessionId"] = Guid.NewGuid().ToString();

        return next();
    }
}

public class GenerateRandomSessionIdForReplies : Behavior<IOutgoingReplyContext>
{
    public override Task Invoke(IOutgoingReplyContext context, Func<Task> next)
    {
        var dispatchProperties = context.Extensions.Get<DispatchProperties>();
        dispatchProperties.TryAdd("SessionId", Guid.NewGuid().ToString());

        return next();
    }
}

public class ConfigureEndpointAzureServiceBusTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_OrderedConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("envvar AzureServiceBus_ConnectionStringOrdered not set");
        }

        var topology = TopicTopology.Default;
        topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());

        var transport = new AzureServiceBusTransport(connectionString, topology);

        if (endpointName.Contains("AuditSpy")
            || endpointName.Contains("AuditSpyEndpoint")
            || endpointName.Contains("audit_with_code_target")
            || endpointName.Contains("EndpointThatHandlesAuditMessages")
            || endpointName.Contains("message_forward_receiver")
            || endpointName.Contains("EndpointThatHandlesErrorMessages")
            || endpointName.Contains("ErrorSpy")
            || endpointName.Contains("error_with_code_source")
            || endpointName.Contains("RetryAckSpy"))
        {
        }
        else
        {
            transport.EnableSessions = true;
        }

        //Use default topology mapping if not overriden by specific test configuration
        if (!ApplyMappingsToSupportMultipleInheritance(endpointName, topology))
        {
            foreach (var eventType in publisherMetadata.Publishers.SelectMany(p => p.Events))
            {
                topology.PublishTo(eventType, eventType.ToTopicName());
                topology.SubscribeTo(eventType, eventType.ToTopicName());
            }
        }

        configuration.UseTransport(transport);

        configuration.EnableTestIndependence();

        configuration.Pipeline.Register("GenerateRandomSessionIdForReplies", typeof(GenerateRandomSessionIdForReplies), "Sets random session ID to all outgoing replies");
        configuration.Pipeline.Register("GenerateRandomSessionIdForSends", typeof(GenerateRandomSessionIdForSends), "Sets random session ID to all outgoing sends");
        configuration.Pipeline.Register("GenerateRandomSessionIdForPublishes", typeof(GenerateRandomSessionIdForPublishes), "Sets random session ID to all outgoing publishes");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    static bool ApplyMappingsToSupportMultipleInheritance(string endpointName, TopicPerEventTopology topology)
    {
        if (endpointName == Conventions.EndpointNamingConvention(typeof(MultiSubscribeToPolymorphicEvent.Subscriber)))
        {
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent1).ToTopicName());
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent2).ToTopicName());
            return true;
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.GeneralSubscriber)))
        {
            topology.SubscribeTo<When_subscribing_to_a_base_event.IBaseEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            topology.SubscribeTo<When_subscribing_to_a_base_event.SpecificEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            return true;
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.Publisher)))
        {
            topology.PublishTo<When_subscribing_to_a_base_event.IBaseEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            topology.PublishTo<When_subscribing_to_a_base_event.SpecificEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            return true;
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
        {
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
            return true;
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
        {
            topology.SubscribeTo<When_started_by_base_event_from_other_saga.IBaseEvent>(
                typeof(When_started_by_base_event_from_other_saga.ISomethingHappenedEvent).ToTopicName());
            return true;
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_multiple_versions_of_a_message_is_published.V1Subscriber)))
        {
            topology.SubscribeTo<When_multiple_versions_of_a_message_is_published.V1Event>(
                typeof(When_multiple_versions_of_a_message_is_published.V2Event).ToTopicName());
            return true;
        }

        return false;
    }

    public Task Cleanup() => Task.CompletedTask;
}
