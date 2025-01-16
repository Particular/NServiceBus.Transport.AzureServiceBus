namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_multi_subscribing_to_a_polymorphic_event_using_topic_mappings : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Both_events_should_be_delivered()
        {
            Requires.NativePubSubSupport();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher1>(b => b.When(c => c.EndpointsStarted, (session, c) =>
                {
                    c.AddTrace("Publishing MyEvent1");
                    return session.Publish(new MyEvent1());
                }))
                .WithEndpoint<Publisher2>(b => b.When(c => c.EndpointsStarted, (session, c) =>
                {
                    c.AddTrace("Publishing MyEvent2");
                    return session.Publish(new MyEvent2());
                }))
                .WithEndpoint<Subscriber>()
                .Done(c => c.SubscriberGotIMyEvent && c.SubscriberGotMyEvent2)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.SubscriberGotIMyEvent, Is.True);
                Assert.That(context.SubscriberGotMyEvent2, Is.True);
            });
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotIMyEvent { get; set; }
            public bool SubscriberGotMyEvent2 { get; set; }
        }

        public class Publisher1 : EndpointConfigurationBuilder
        {
            public Publisher1()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var topology = TopicTopology.Default;
                    topology.PublishTo<MyEvent1>(ConfigureEndpointAzureServiceBusTransport.GetTopicName(typeof(MyEvent1)));

                    c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent1>(this));
            }
        }

        public class Publisher2 : EndpointConfigurationBuilder
        {
            public Publisher2()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var topology = TopicTopology.Default;
                    topology.PublishTo<MyEvent2>(ConfigureEndpointAzureServiceBusTransport.GetTopicName(typeof(MyEvent2)));

                    c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent2>(this));
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var topology = TopicTopology.Default;
                    topology.SubscribeTo<IMyEvent>(ConfigureEndpointAzureServiceBusTransport.GetTopicName(typeof(MyEvent1)));
                    topology.SubscribeTo<IMyEvent>(ConfigureEndpointAzureServiceBusTransport.GetTopicName(typeof(MyEvent2)));

                    c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<MyEvent1, Publisher1>();
                    metadata.RegisterPublisherFor<MyEvent2, Publisher2>();
                    metadata.RegisterPublisherFor<IMyEvent>("not-used");
                });
            }

            public class MyHandler : IHandleMessages<IMyEvent>
            {
                Context testContext;

                public MyHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(IMyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.AddTrace($"Got event '{messageThatIsEnlisted}'");
                    if (messageThatIsEnlisted is MyEvent2)
                    {
                        testContext.SubscriberGotMyEvent2 = true;
                    }
                    else
                    {
                        testContext.SubscriberGotIMyEvent = true;
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent1 : IMyEvent
        {
        }

        public class MyEvent2 : IMyEvent
        {
        }

        public interface IMyEvent : IEvent
        {
        }
    }
}