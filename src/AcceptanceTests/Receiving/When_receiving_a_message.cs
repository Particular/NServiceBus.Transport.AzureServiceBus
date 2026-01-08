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
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(
                    (session, c) => session.SendLocal(new Message())))
                .Run();

            Assert.That(context.NativeMessageFound, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool NativeMessageFound { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServer>((c, d) =>
                    c.Pipeline.Register(b => new CheckContextForValidUntilUtc((Context)d.ScenarioContext), "Behavior to validate context bag contains the original brokered message"));

            public class Handler(Context testContext) : IHandleMessages<Message>
            {
                public Task Handle(Message request, IMessageHandlerContext context)
                {
                    testContext.NativeMessageFound = testContext.NativeMessageFound && context.Extensions.Get<ServiceBusReceivedMessage>() != null;
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }

            public class CheckContextForValidUntilUtc(Context testContext) : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    testContext.NativeMessageFound = context.Extensions.Get<ServiceBusReceivedMessage>() != null;

                    return next();
                }
            }
        }

        public class Message : IMessage;
    }
}