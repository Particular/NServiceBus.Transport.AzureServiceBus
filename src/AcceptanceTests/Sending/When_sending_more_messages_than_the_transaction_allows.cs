namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_more_messages_than_the_transaction_allows : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_move_message_to_error_queue()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.SendLocal(new KickOffMessage())).DoNotFailOnErrorMessages())
                .WithEndpoint<Receiver>()
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageMovedToTheErrorQueue)
                .Run();

            // no messages should leak
            Assert.IsEmpty(context.MessageIdsReceived);
        }

        public class Context : ScenarioContext
        {
            public ConcurrentBag<string> MessageIdsReceived { get; } = new();
            public bool MessageMovedToTheErrorQueue { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));

                    var recoverability = c.Recoverability();
                    recoverability.Immediate(i => i.NumberOfRetries(0));
                    recoverability.Delayed(d => d.NumberOfRetries(0));
                    c.SendFailedMessagesTo<ErrorSpy>();
                });

            public class KickOffHandler : IHandleMessages<KickOffMessage>
            {
                public async Task Handle(KickOffMessage message, IMessageHandlerContext context)
                {
                    for (int i = 0; i < 500; i++)
                    {
                        await context.Send(new MyMessage());
                    }
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageIdsReceived.Add(context.MessageId);
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<KickOffMessage>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(KickOffMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageMovedToTheErrorQueue = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class KickOffMessage : ICommand
        {
        }

        public class MyMessage : ICommand
        {
        }
    }
}