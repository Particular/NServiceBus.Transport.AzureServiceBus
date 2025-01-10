namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_publishing_sendonly_and_subscribing_on_different_topics : NServiceBusAcceptanceTest
    {
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
                    MigrationTopology transportTopology = TopicTopology.Single("bundle-a");
                    transportTopology.SubscribeToDefaultTopic<MyEvent>();
                    transport.Topology = transportTopology;
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
                    MigrationTopology transportTopology = TopicTopology.Hierarchy("bundle-a", "bundle-b");
                    transportTopology.SubscribeToDefaultTopic<MyEvent>();
                    transport.Topology = transportTopology;
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
                    MigrationTopology transportTopology = TopicTopology.Hierarchy("bundle-a", "bundle-c");
                    transportTopology.SubscribeToDefaultTopic<MyEvent>();
                    transport.Topology = transportTopology;
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