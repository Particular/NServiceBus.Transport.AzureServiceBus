namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_receiving_a_message
    {
        [Test]
        public async Task Should_have_access_to_the_native_message_via_extensions()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(
                    (session, c) => session.SendLocal(new Message())))
                .Done(c => c.NativeMessageFound)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool NativeMessageFound { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>((c, d) =>
                    c.Pipeline.Register(b => new CheckContextForValidUntilUtc((Context)d.ScenarioContext), "Behavior to validate context bag contains the original brokered message"));
            }

            public class Handler : IHandleMessages<Message>
            {
                Context testContext;

                public Handler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message request, IMessageHandlerContext context)
                {
                    testContext.NativeMessageFound = testContext.NativeMessageFound && context.Extensions.Get<ServiceBusReceivedMessage>() != null;

                    return Task.CompletedTask;
                }
            }

            public class CheckContextForValidUntilUtc : Behavior<ITransportReceiveContext>
            {
                readonly Context testContext;

                public CheckContextForValidUntilUtc(Context context)
                {
                    testContext = context;
                }

                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    testContext.NativeMessageFound = context.Extensions.Get<ServiceBusReceivedMessage>() != null;

                    return next();
                }
            }
        }

        public class Message : IMessage { }
    }
}