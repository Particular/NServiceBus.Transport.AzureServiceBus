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
                    transport.Topology = TopicTopology.Single("bundle-a");
                });

            public class MyHandler : IHandleMessages<MyCommand>
            {
                public Task Handle(MyCommand message, IMessageHandlerContext context)
                    => context.Publish(new EventFromTopicA());
            }

            public class EventFromTopicBHandler : IHandleMessages<EventFromTopicB>
            {
                public EventFromTopicBHandler(Context context) => testContext = context;

                public Task Handle(EventFromTopicB message, IMessageHandlerContext context)
                {
                    testContext.PublisherGotEventFromSubscriberOnTopicB = true;
                    return Task.CompletedTask;
                }

                Context testContext;
            }

            public class EventFromTopicCHandler : IHandleMessages<EventFromTopicC>
            {
                public EventFromTopicCHandler(Context context) => testContext = context;

                public Task Handle(EventFromTopicC message, IMessageHandlerContext context)
                {
                    testContext.PublisherGotEventFromSubscriberOnTopicC = true;
                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        public class SubscriberOnTopicB : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicB()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    transport.Topology = TopicTopology.Hierarchy("bundle-a", "bundle-b");
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
                    transport.Topology = TopicTopology.Hierarchy("bundle-a", "bundle-c");
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