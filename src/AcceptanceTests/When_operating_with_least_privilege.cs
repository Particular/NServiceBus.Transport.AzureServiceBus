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

public class When_operating_with_least_privilege : NServiceBusAcceptanceTest
{
    const string DedicatedTopic = "bundle-no-manage-rights";

    [SetUp]
    public async Task Setup()
    {
        var adminClient =
            new ServiceBusAdministrationClient(
                Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
        try
        {
            // makes sure during local development the topic gets cleared before each test run
            await adminClient.DeleteTopicAsync(DedicatedTopic);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }
    }

    [Test]
    public async Task Should_allow_message_operations_on_existing_resources()
    {
        // Run the scenario first with manage rights to make sure the topic and the subscription is created
        await Scenario.Define<Context>()
            .WithEndpoint<Publisher>()
            .WithEndpoint<Subscriber>()
            .Done(c => c.EndpointsStarted)
            .Run();

        // Now we run without manage rights
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b =>
            {
                b.CustomConfig(c =>
                {
                    // least-privilege mode doesn't support installers
                    c.GetSettings().Set("Installers.Enable", false);
                    // AutoSubscribe is not supported in least-privilege mode
                    c.DisableFeature<AutoSubscribe>();

                    var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                    transport.ConnectionString =
                        Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString_Restricted");
                });
                b.When(session => session.SendLocal(new MyCommand()));
            })
            .WithEndpoint<Subscriber>(b =>
            {
                b.CustomConfig(c =>
                {
                    // least-privilege mode doesn't support installers
                    c.GetSettings().Set("Installers.Enable", false);
                    // AutoSubscribe is not supported in least-privilege mode
                    c.DisableFeature<AutoSubscribe>();

                    var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                    transport.ConnectionString =
                        Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString_Restricted");
                });
            })
            .Run();

        Assert.That(context.SubscriberGotEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool SubscriberGotEvent { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultPublisher>(b =>
            {
            }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));

        public class MyHandler : IHandleMessages<MyCommand>
        {
            public Task Handle(MyCommand message, IMessageHandlerContext context)
                => context.Publish(new MyEvent());
        }
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber()
            => EndpointSetup<DefaultServer>(b =>
            {
            }, metadata => metadata.RegisterPublisherFor<MyEvent, Publisher>());

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

    public class MyCommand : ICommand;
}