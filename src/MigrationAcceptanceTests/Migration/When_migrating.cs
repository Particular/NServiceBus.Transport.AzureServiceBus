namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.AzureServiceBus.AcceptanceTests;

    public class When_migrating : NServiceBusAcceptanceTest
    {
        const string bundleTopicName = "bundle-m";

        [SetUp]
        public async Task Setup()
        {
            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync(bundleTopicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        [Test]
        public async Task Should_not_lose_any_events()
        {
            //Before migration begins
            var beforeMigration = await Scenario.Define<Context>(c => c.Step = "Before migration")
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        var topology = TopicTopology.MigrateFromNamedSingleTopic(bundleTopicName);
#pragma warning restore CS0618 // Type or member is obsolete
                        topology.EventToMigrate<MyEvent>();

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                    b.When((session, ctx) => session.Publish(new MyEvent()));
                })
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        var topology = TopicTopology.MigrateFromNamedSingleTopic(bundleTopicName);
#pragma warning restore CS0618 // Type or member is obsolete
                        topology.EventToMigrate<MyEvent>(ruleNameOverride: typeof(MyEvent).FullName.Shorten());

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(60));

            Assert.That(beforeMigration.GotTheEvent, Is.True);

            /*
             * When auto-subscribe enabled, Subscriber creates a new topic/subscription
             * but continues to receive events via the bundle topic
             */
            var subscriberMigrated = await Scenario.Define<Context>(c => c.Step = "Subscriber migrated")
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        var topology = TopicTopology.MigrateFromNamedSingleTopic(bundleTopicName);
#pragma warning restore CS0618 // Type or member is obsolete
                        topology.EventToMigrate<MyEvent>();

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                    b.When((session, ctx) => session.Publish(new MyEvent()));
                })
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var topology = TopicTopology.Default;
                        topology.SubscribeTo<MyEvent>("my-event");

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(60));

            Assert.That(subscriberMigrated.GotTheEvent, Is.True);

            //Make sure the bundle topic does not exist and the event is delivered on the new path
            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync(bundleTopicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }

            //Event delivery path switched to new once publisher changes config
            var topicMigrated = await Scenario.Define<Context>(c => c.Step = "Topic migrated")
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var topology = TopicTopology.Default;
                        topology.PublishTo<MyEvent>("my-event");

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                    b.When((session, ctx) => session.Publish(new MyEvent()));
                })
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var topology = TopicTopology.Default;
                        topology.SubscribeTo<MyEvent>("my-event");

                        c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(60));

            Assert.That(topicMigrated.GotTheEvent, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public string Step { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
                EndpointSetup<DefaultServer>((c, rd) =>
                {
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() =>
                EndpointSetup<DefaultServer>((c, rd) =>
                {
                }, metadata => metadata.RegisterPublisherFor<MyEvent, Publisher>());

            public class MyEventMessageHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent;
    }
}