namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_a_topic_using_hierarchy_namespace : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_publish_and_subscribe()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                    {
                        b.CustomConfig(cfg =>
                        {
                            var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                            //transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                        });
                        b.When(c => c.SubscriptionComplete, (session, c) =>
                            {
                                c.AddTrace("Subscription is complete, going to publish MyEvent");
                                return session.Publish(new MyEvent());
                            }
                        );
                    }
        )
        .WithEndpoint<HierarchySubscriber>(b => b.When(async (session, ctx) =>
                {
                    await session.Subscribe<MyEvent>();
                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.SubscriptionComplete = true;
                        ctx.AddTrace("Subscriber is now subscribed (at least we have asked the broker to be subscribed)");
                    }
                    else
                    {
                        ctx.AddTrace("Subscriber has now asked to be subscribed to MyEvent");
                    }
                }))
                .Run();
            Assert.That(context.HierarchyMessageReceived, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool HierarchyMessageReceived { get; set; }
            public bool SubscriptionComplete { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
                EndpointSetup<DefaultPublisher>(b => b.OnEndpointSubscribed<Context>((s, context) =>
                {
                    var subscriber = Conventions.EndpointNamingConvention(typeof(HierarchySubscriber));
                    if (s.SubscriberEndpoint.Contains(subscriber))
                    {
                        context.SubscriptionComplete = true;
                        context.AddTrace($"{subscriber} is now subscribed");
                    }
                }), metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
        }

        public class HierarchySubscriber : EndpointConfigurationBuilder
        {
            public HierarchySubscriber() =>
                EndpointSetup<DefaultServer>(c => c.DisableFeature<AutoSubscribe>(),
                    metadata => metadata.RegisterPublisherFor<MyEvent, Publisher>());

            public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }
        }


        public class MyEvent : IEvent;
    }
}