namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_publishing_subscribing_on_different_topics : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_be_delivered_to_all_subscribers()
        {
            Requires.NativePubSubSupport();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyPublisher>(b => b.When((session, c) => session.Publish(new MyEvent())))
                .WithEndpoint<SubscriberOnTopicB>()
                .WithEndpoint<SubscriberOnTopicC>()
                .Done(c => c.SubscriberOnTopicAGotTheEvent && c.SubscriberOnTopicBGotTheEvent)
                .Run();

            Assert.True(context.SubscriberOnTopicAGotTheEvent);
            Assert.True(context.SubscriberOnTopicBGotTheEvent);
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberOnTopicAGotTheEvent { get; set; }
            public bool SubscriberOnTopicBGotTheEvent { get; set; }
        }

        public class SendOnlyPublisher : EndpointConfigurationBuilder
        {
            public SendOnlyPublisher() =>
                EndpointSetup<DefaultPublisher>(b =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    transport.TopicNameToPublishTo = "bundle-a";
                    b.SendOnly();
                });
        }

        public class SubscriberOnTopicB : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicB()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    transport.TopicNameToPublishTo = "bundle-a";
                    transport.TopicNameToSubscribeOn = "bundle-b";
                });

            public class MyHandler : IHandleMessages<MyEvent>
            {
                public MyHandler(Context context) => testContext = context;

                public Task Handle(MyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.SubscriberOnTopicAGotTheEvent = true;

                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        public class SubscriberOnTopicC : EndpointConfigurationBuilder
        {
            public SubscriberOnTopicC()
                => EndpointSetup<DefaultServer>(b
                    =>
                {
                    var transport = b.ConfigureTransport<AzureServiceBusTransport>();
                    transport.TopicNameToPublishTo = "bundle-a";
                    transport.TopicNameToSubscribeOn = "bundle-c";
                });

            public class MyHandler : IHandleMessages<MyEvent>
            {
                public MyHandler(Context context) => testContext = context;

                public Task Handle(MyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.SubscriberOnTopicBGotTheEvent = true;

                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}