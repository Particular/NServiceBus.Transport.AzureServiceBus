using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Feature;
using NServiceBus.AcceptanceTests.Routing;
using NServiceBus.AcceptanceTests.Routing.NativePublishSubscribe;
using NServiceBus.AcceptanceTests.Sagas;
using NServiceBus.AcceptanceTests.Versioning;
using NServiceBus.Pipeline;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class GenerateRandomSessionIdForSends : Behavior<IOutgoingSendContext>
{
    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        if (context.Headers.TryGetValue(Headers.IsSagaTimeoutMessage, out var isSagaTimeoutMessage) &&
            bool.TryParse(isSagaTimeoutMessage, out var isSagaTimeout) &&
            isSagaTimeout)
        {
            return next();
        }

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
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("envvar AzureServiceBus_ConnectionString not set");
        }

        var topology = TopicTopology.Default;
        topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());

        var transport = new AzureServiceBusTransport(connectionString, topology);
        AcceptanceTestEntityNames.Apply(transport);

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
        var subscribedEvents = ApplyTestSpecificMappings(endpointName, topology) ?? ApplyDefaultMappings(endpointName, topology, publisherMetadata);

        configuration.UseTransport(transport);
        SubscribeBeforeReceivingWithoutAutoSubscribe.Enable(configuration, subscribedEvents);

        configuration.EnableTestIndependence();

        configuration.Pipeline.Register("GenerateRandomSessionIdForReplies", typeof(GenerateRandomSessionIdForReplies), "Sets random session ID to all outgoing replies");
        configuration.Pipeline.Register("GenerateRandomSessionIdForSends", typeof(GenerateRandomSessionIdForSends), "Sets random session ID to all outgoing sends");
        configuration.Pipeline.Register("GenerateRandomSessionIdForPublishes", typeof(GenerateRandomSessionIdForPublishes), "Sets random session ID to all outgoing publishes");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    // Returns the subscribed event types
    static Type[] ApplyDefaultMappings(string endpointName, TopicPerEventTopology topology, PublisherMetadata publisherMetadata)
    {
        var subscribedEvents = new List<Type>();
        foreach (var publisher in publisherMetadata.Publishers)
        {
            var publishedByThisEndpoint = publisher.PublisherName == endpointName;
            foreach (var eventType in publisher.Events)
            {
                topology.PublishTo(eventType, eventType.ToTopicName());

                // In session mode every subscription mapping starts a forwarder, which fails when nothing creates the subscription.
                // A publisher that handles the events it publishes itself needs a test-specific mapping.
                if (!publishedByThisEndpoint)
                {
                    topology.SubscribeTo(eventType, eventType.ToTopicName());
                    subscribedEvents.Add(eventType);
                }
            }
        }

        return [.. subscribedEvents];
    }

    // Mappings for endpoints the default mapping does not fit: subscribing through multiple inheritance, or publishers that handle their own events.
    // Returns the subscribed event types, or null when the default mapping applies.
    static Type[] ApplyTestSpecificMappings(string endpointName, TopicPerEventTopology topology)
    {
        if (endpointName == Conventions.EndpointNamingConvention(typeof(MultiSubscribeToPolymorphicEvent.Subscriber)))
        {
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent1).ToTopicName());
            topology.SubscribeTo<MultiSubscribeToPolymorphicEvent.IMyEvent>(typeof(MultiSubscribeToPolymorphicEvent.MyEvent2).ToTopicName());
            return [typeof(MultiSubscribeToPolymorphicEvent.IMyEvent)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.GeneralSubscriber)))
        {
            topology.SubscribeTo<When_subscribing_to_a_base_event.IBaseEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            topology.SubscribeTo<When_subscribing_to_a_base_event.SpecificEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            return [typeof(When_subscribing_to_a_base_event.IBaseEvent)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.Publisher)))
        {
            topology.PublishTo<When_subscribing_to_a_base_event.IBaseEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            topology.PublishTo<When_subscribing_to_a_base_event.SpecificEvent>(typeof(When_subscribing_to_a_base_event.SpecificEvent).ToTopicName());
            return [];
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
        {
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
            topology.SubscribeTo<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB>(
                typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent).ToTopicName());
            return [typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA), typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
        {
            topology.SubscribeTo<When_started_by_base_event_from_other_saga.IBaseEvent>(
                typeof(When_started_by_base_event_from_other_saga.ISomethingHappenedEvent).ToTopicName());
            return [typeof(When_started_by_base_event_from_other_saga.IBaseEvent)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(
                typeof(When_multiple_versions_of_a_message_is_published.V1Subscriber)))
        {
            topology.SubscribeTo<When_multiple_versions_of_a_message_is_published.V1Event>(
                typeof(When_multiple_versions_of_a_message_is_published.V2Event).ToTopicName());
            return [typeof(When_multiple_versions_of_a_message_is_published.V1Event)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_in_FST.EndpointWithStartupTask)))
        {
            topology.PublishTo<When_subscribing_in_FST.LocalEvent>(typeof(When_subscribing_in_FST.LocalEvent).ToTopicName());
            topology.SubscribeTo<When_subscribing_in_FST.LocalEvent>(typeof(When_subscribing_in_FST.LocalEvent).ToTopicName());
            return [typeof(When_subscribing_in_FST.LocalEvent)];
        }

        if (endpointName == Conventions.EndpointNamingConvention(typeof(When_customizing_an_outgoing_native_message.Endpoint)))
        {
            topology.PublishTo<When_customizing_an_outgoing_native_message.MessageSessionPublishedEvent>(
                typeof(When_customizing_an_outgoing_native_message.MessageSessionPublishedEvent).ToTopicName());
            topology.SubscribeTo<When_customizing_an_outgoing_native_message.MessageSessionPublishedEvent>(
                typeof(When_customizing_an_outgoing_native_message.MessageSessionPublishedEvent).ToTopicName());
            topology.PublishTo<When_customizing_an_outgoing_native_message.MessageHandlerContextPublishedEvent>(
                typeof(When_customizing_an_outgoing_native_message.MessageHandlerContextPublishedEvent).ToTopicName());
            topology.SubscribeTo<When_customizing_an_outgoing_native_message.MessageHandlerContextPublishedEvent>(
                typeof(When_customizing_an_outgoing_native_message.MessageHandlerContextPublishedEvent).ToTopicName());
            return [typeof(When_customizing_an_outgoing_native_message.MessageSessionPublishedEvent), typeof(When_customizing_an_outgoing_native_message.MessageHandlerContextPublishedEvent)];
        }

        return null;
    }

    public Task Cleanup() => Task.CompletedTask;
}