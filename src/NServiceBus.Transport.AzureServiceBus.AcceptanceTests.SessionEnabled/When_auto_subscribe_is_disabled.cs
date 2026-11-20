namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Features;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Configuration.AdvancedExtensibility;
using NUnit.Framework;

public class When_auto_subscribe_is_disabled : NServiceBusAcceptanceTest
{
    const string TopicWithoutSubscription = "topic-without-subscription";

    [SetUp]
    public async Task Setup()
    {
        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());
        try
        {
            // Entities outlive local test runs
            await adminClient.DeleteTopicAsync(AcceptanceTestEntityNames.For(TopicWithoutSubscription));
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }
    }

    [Test]
    public void Should_fail_to_start_when_a_mapped_subscription_does_not_exist()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Scenario.Define<Context>()
            .WithEndpoint<EndpointWithoutSubscription>()
            .Done(c => c.EndpointsStarted)
            .Run());

        Assert.That(exception.Message, Does.Contain("does not exist on topics").And.Contain(AcceptanceTestEntityNames.For(TopicWithoutSubscription)));
    }

    [Test]
    public async Task Should_forward_from_mapped_subscriptions_that_exist()
    {
        // Auto-subscribe creates the subscription, like a deployment would
        await Scenario.Define<Context>()
            .WithEndpoint<Publisher>()
            .WithEndpoint<Subscriber>()
            .Done(c => c.EndpointsStarted)
            .Run();

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(session => session.Publish(new MyEvent())))
            .WithEndpoint<Subscriber>(b => b.CustomConfig(c =>
            {
                c.DisableFeature<AutoSubscribe>();
                // Like a production deployment, so nothing creates the subscription
                c.GetSettings().Set("Installers.Enable", false);
            }))
            .Run();

        Assert.That(context.SubscriberGotEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool SubscriberGotEvent { get; set; }
    }

    public class EndpointWithoutSubscription : EndpointConfigurationBuilder
    {
        public EndpointWithoutSubscription() =>
            EndpointSetup<DefaultServer>(c =>
            {
                c.DisableFeature<AutoSubscribe>();

                var topology = (TopicPerEventTopology)c.ConfigureTransport<AzureServiceBusTransport>().Topology;
                topology.SubscribeTo<MyEvent>(TopicWithoutSubscription);
            });
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultPublisher>(_ => { }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() =>
            EndpointSetup<DefaultServer>(_ => { }, metadata => metadata.RegisterPublisherFor<MyEvent, Publisher>());

        [Handler]
        public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                testContext.SubscriberGotEvent = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}
