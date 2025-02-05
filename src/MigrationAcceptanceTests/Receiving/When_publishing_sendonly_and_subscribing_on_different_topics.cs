namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_publishing_sendonly_and_subscribing_on_different_topics : NServiceBusAcceptanceTest
    {
        [SetUp]
        public async Task Setup()
        {
            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync("bundle-a");
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }

            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync("bundle-b");
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }

            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync("bundle-c");
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        [Test]
        public async Task Should_be_delivered_to_all_subscribers()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyPublisherOnTopicA>(b => b.When((session, c) => session.Publish(new MyEvent())))
                .WithEndpoint<SubscriberOnTopicB>()
                .WithEndpoint<SubscriberOnTopicC>()
                .Done(c => c.SubscriberOnTopicAGotTheEvent && c.SubscriberOnTopicBGotTheEvent)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.SubscriberOnTopicAGotTheEvent, Is.True);
                Assert.That(context.SubscriberOnTopicBGotTheEvent, Is.True);
            });
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberOnTopicAGotTheEvent { get; set; }
            public bool SubscriberOnTopicBGotTheEvent { get; set; }
        }

        public class SendOnlyPublisherOnTopicA : EndpointConfigurationBuilder
        {
            public SendOnlyPublisherOnTopicA() =>
                EndpointSetup<DefaultPublisher>(b =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    MigrationTopology topology = TopicTopology.MigrateFromNamedSingleTopic("bundle-a");
                    topology.EventToMigrate<MyEvent>();
                    transport.Topology = topology;
                    b.SendOnly();
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
        }

        public class SubscriberOnTopicB : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicB()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    MigrationTopology topology = TopicTopology.MigrateFromTopicHierarchy("bundle-a", "bundle-b");
                    string endpointName = Conventions.EndpointNamingConvention(typeof(SubscriberOnTopicB));
                    topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                    topology.EventToMigrate<MyEvent>(ruleNameOverride: typeof(MyEvent).FullName.Shorten());
                    transport.Topology = topology;
                }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(SendOnlyPublisherOnTopicA)));

            public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.SubscriberOnTopicAGotTheEvent = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class SubscriberOnTopicC : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicC()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    MigrationTopology topology = TopicTopology.MigrateFromTopicHierarchy("bundle-a", "bundle-c");
                    string endpointName = Conventions.EndpointNamingConvention(typeof(SubscriberOnTopicC));
                    topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                    topology.EventToMigrate<MyEvent>(ruleNameOverride: typeof(MyEvent).FullName.Shorten());
                    transport.Topology = topology;
                }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(SendOnlyPublisherOnTopicA)));

            public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.SubscriberOnTopicBGotTheEvent = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent;
    }
}