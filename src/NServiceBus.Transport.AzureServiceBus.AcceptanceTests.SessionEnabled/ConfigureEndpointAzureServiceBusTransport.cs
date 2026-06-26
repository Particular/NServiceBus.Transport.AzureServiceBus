using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Pipeline;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

public class GenerateRandomSessionIdForSends : Behavior<IOutgoingSendContext>
{
    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
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
        dispatchProperties["SessionId"] = Guid.NewGuid().ToString();

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

        foreach (var eventType in publisherMetadata.Publishers.SelectMany(p => p.Events))
        {
            topology.PublishTo(eventType, eventType.ToTopicName());
            topology.SubscribeTo(eventType, eventType.ToTopicName());
        }

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

        configuration.UseTransport(transport);

        configuration.EnableTestIndependence();

        configuration.Pipeline.Register("GenerateRandomSessionIdForReplies", typeof(GenerateRandomSessionIdForReplies), "Sets random session ID to all outgoing replies");
        configuration.Pipeline.Register("GenerateRandomSessionIdForSends", typeof(GenerateRandomSessionIdForSends), "Sets random session ID to all outgoing sends");

        configuration.EnforcePublisherMetadataRegistration(endpointName, publisherMetadata);

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;
}
