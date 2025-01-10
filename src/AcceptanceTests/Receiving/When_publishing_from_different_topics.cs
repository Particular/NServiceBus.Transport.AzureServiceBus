namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    // Makes sure we have enough forwarding hops available to support the hierarchy
    public class When_publishing_from_different_topics : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_be_delivered_to_all_subscribers_and_back_to_the_publisher()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<PublisherOnTopicA>(b => b.When((session, c) => session.SendLocal(new MyCommand())))
                .WithEndpoint<SubscriberOnTopicB>()
                .WithEndpoint<SubscriberOnTopicC>()
                .Done(c => c.PublisherGotEventFromSubscriberOnTopicB && c.PublisherGotEventFromSubscriberOnTopicC)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.PublisherGotEventFromSubscriberOnTopicB, Is.True);
                Assert.That(context.PublisherGotEventFromSubscriberOnTopicC, Is.True);
            });
        }

        public class Context : ScenarioContext
        {
            public bool PublisherGotEventFromSubscriberOnTopicB { get; set; }
            public bool PublisherGotEventFromSubscriberOnTopicC { get; set; }
        }

        public class PublisherOnTopicA : EndpointConfigurationBuilder
        {
            public PublisherOnTopicA() =>
                EndpointSetup<DefaultPublisher>(b =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    var topology = TopicTopology.Single("bundle-a");
                    topology.SubscribeToDefaultTopic<EventFromTopicB>();
                    topology.SubscribeToDefaultTopic<EventFromTopicC>();
                    topology.PublishToDefaultTopic<EventFromTopicA>();
                    transport.Topology = topology;
                }, metadata =>
                {
                    metadata.RegisterSelfAsPublisherFor<EventFromTopicA>(this);
                });

            public class MyHandler : IHandleMessages<MyCommand>
            {
                public Task Handle(MyCommand message, IMessageHandlerContext context)
                    => context.Publish(new EventFromTopicA());
            }

            public class EventFromTopicBHandler(Context testContext) : IHandleMessages<EventFromTopicB>
            {
                public Task Handle(EventFromTopicB message, IMessageHandlerContext context)
                {
                    testContext.PublisherGotEventFromSubscriberOnTopicB = true;
                    return Task.CompletedTask;
                }
            }

            public class EventFromTopicCHandler(Context testContext) : IHandleMessages<EventFromTopicC>
            {
                public Task Handle(EventFromTopicC message, IMessageHandlerContext context)
                {
                    testContext.PublisherGotEventFromSubscriberOnTopicC = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class SubscriberOnTopicB : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicB()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    var topology = TopicTopology.Hierarchy("bundle-a", "bundle-b");
                    topology.SubscribeToDefaultTopic<EventFromTopicA>();
                    topology.PublishToDefaultTopic<EventFromTopicB>();
                    transport.Topology = topology;
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<EventFromTopicA>(typeof(PublisherOnTopicA));
                    metadata.RegisterSelfAsPublisherFor<EventFromTopicB>(this);
                });

            public class MyHandler : IHandleMessages<EventFromTopicA>
            {
                public Task Handle(EventFromTopicA messageThatIsEnlisted, IMessageHandlerContext context)
                    => context.Publish(new EventFromTopicB());
            }
        }

        public class SubscriberOnTopicC : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicC()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    var topology = TopicTopology.Hierarchy("bundle-a", "bundle-c");
                    topology.SubscribeToDefaultTopic<EventFromTopicA>();
                    topology.PublishToDefaultTopic<EventFromTopicC>();
                    transport.Topology = topology;
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<EventFromTopicA>(typeof(PublisherOnTopicA));
                    metadata.RegisterSelfAsPublisherFor<EventFromTopicC>(this);
                });

            public class MyHandler : IHandleMessages<EventFromTopicA>
            {
                public Task Handle(EventFromTopicA messageThatIsEnlisted, IMessageHandlerContext context)
                    => context.Publish(new EventFromTopicC());
            }
        }

        public class EventFromTopicA : IEvent
        {
        }

        public class EventFromTopicB : IEvent
        {
        }

        public class EventFromTopicC : IEvent
        {
        }

        public class MyCommand : ICommand
        {
        }
    }
}