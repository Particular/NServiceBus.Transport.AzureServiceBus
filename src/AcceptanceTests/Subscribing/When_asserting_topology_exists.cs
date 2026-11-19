namespace NServiceBus.AcceptanceTests.NativePubSub;

using System;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using Azure.Messaging.ServiceBus.Administration;
using Logging;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Configuration.AdvancedExtensibility;
using NUnit.Framework;
using Transport.AzureServiceBus;
using Transport.AzureServiceBus.AcceptanceTests;

public class When_asserting_topology_exists : NServiceBusAcceptanceTest
{
    [SetUp]
    public async Task Setup() => await CleanupTopic();

    [TearDown]
    public async Task Teardown() => await CleanupTopic();

    static async Task CleanupTopic()
    {
        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());
        var topicName = AcceptanceTestEntityNames.For(typeof(MyEvent).ToTopicName());

        if (await adminClient.TopicExistsAsync(topicName))
        {
            await adminClient.DeleteTopicAsync(topicName);
        }
    }

    [Test]
    public async Task Should_receive_events_through_existing_subscriptions()
    {
        Requires.NativePubSubSupport();

        // First run with manage rights so the topic and the subscription exist, like a provisioning script
        await Scenario.Define<Context>()
            .WithEndpoint<Publisher>()
            .WithEndpoint<Subscriber>()
            .Done(c => c.EndpointsStarted)
            .Run();

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b =>
            {
                b.CustomConfig(UseAssertTopologyExists);
                b.When(session => session.Publish(new MyEvent()));
            })
            .WithEndpoint<Subscriber>(b => b.CustomConfig(UseAssertTopologyExists))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.SubscriberGotEvent, Is.True);
            Assert.That(context.Logs.Where(l => l.Level >= LogLevel.Warn && !l.Message.StartsWith("The host ID is generated using MD5")).Select(l => l.Message), Is.Empty);
        }
    }

    [Test]
    public async Task Should_fail_to_start_when_a_subscription_does_not_exist()
    {
        Requires.NativePubSubSupport();

        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());
        await adminClient.CreateTopicAsync(AcceptanceTestEntityNames.For(typeof(MyEvent).ToTopicName()));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Scenario.Define<Context>()
            .WithEndpoint<Subscriber>(b => b.CustomConfig(UseAssertTopologyExists))
            .Done(c => c.EndpointsStarted)
            .Run());

        Assert.That(exception!.Message, Does.Contain("must exist before the endpoint subscribes").And.Contain(AcceptanceTestEntityNames.For(typeof(MyEvent).ToTopicName())));
    }

    static void UseAssertTopologyExists(EndpointConfiguration c)
    {
        c.GetSettings().Set("Installers.Enable", false);
        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
        transport.ConnectionString = AcceptanceTestConnectionString.RestrictedConnectionString;
        transport.AssertTopologyExists = true;
    }

    public class Context : ScenarioContext
    {
        public bool SubscriberGotEvent { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultServer>(_ => { }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
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
