namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.AzureServiceBus.AcceptanceTests;

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
                .Done(c => c.SubscriberGotMyEvent1 && c.SubscriberGotMyEvent2)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.SubscriberGotMyEvent1, Is.True);
                Assert.That(context.SubscriberGotMyEvent2, Is.True);
            });
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotMyEvent1 { get; set; }
            public bool SubscriberGotMyEvent2 { get; set; }
        }

        public class Publisher1 : EndpointConfigurationBuilder
        {
            public Publisher1() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.ConfigureTransport<AzureServiceBusTransport>();

                    var topology = TopicTopology.Default;
                    topology.PublishTo<MyEvent1>(typeof(MyEvent1).ToTopicName());
                    transport.Topology = topology;
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent1>(this));
        }

        public class Publisher2 : EndpointConfigurationBuilder
        {
            public Publisher2() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.ConfigureTransport<AzureServiceBusTransport>();

                    var topology = TopicTopology.Default;
                    topology.PublishTo<MyEvent2>(typeof(MyEvent2).ToTopicName());
                    transport.Topology = topology;
                }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent2>(this));
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var topology = TopicTopology.Default;
                    topology.SubscriptionName = Conventions.EndpointNamingConvention(typeof(Subscriber)).Shorten();
                    topology.SubscribeTo<IMyEvent>(typeof(MyEvent1).ToTopicName());
                    topology.SubscribeTo<IMyEvent>(typeof(MyEvent2).ToTopicName());

                    c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<MyEvent1, Publisher1>();
                    metadata.RegisterPublisherFor<MyEvent2, Publisher2>();
                    metadata.RegisterPublisherFor<IMyEvent>("not-used");
                });

            public class MyHandler(Context testContext) : IHandleMessages<IMyEvent>
            {
                public Task Handle(IMyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.AddTrace($"Got event '{messageThatIsEnlisted}'");
                    switch (messageThatIsEnlisted)
                    {
                        case MyEvent1:
                            testContext.SubscriberGotMyEvent1 = true;
                            break;
                        case MyEvent2:
                            testContext.SubscriberGotMyEvent2 = true;
                            break;
                        default:
                            break;
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent1 : IMyEvent;

        public class MyEvent2 : IMyEvent;

        public interface IMyEvent : IEvent;
    }
}