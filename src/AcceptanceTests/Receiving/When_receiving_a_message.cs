namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending.Receiving
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_receiving_a_message
    {
        [Test]
        public async Task Should_have_access_to_the_original_brokered_message_via_the_context_bag()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(
                    (session, c) => session.SendLocal(new Message())))
                .Done(c => c.LockedUntilUtcFromHandler == c.LockedUntilUtcFromBehavior)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public DateTime LockedUntilUtcFromHandler { get; set; }
            public DateTime LockedUntilUtcFromBehavior { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>((c, d) =>
                    c.Pipeline.Register(b => new CheckContextForValidUntilUtc(d.ScenarioContext as Context), "Behavior to validate context bag contains the original brokered message"));
            }

            public class Handler : IHandleMessages<Message>
            {
                public Context TestContext { get; set; }

                public Task Handle(Message request, IMessageHandlerContext context)
                {
                    TestContext.LockedUntilUtcFromHandler = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>().SystemProperties.LockedUntilUtc;

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
                    testContext.LockedUntilUtcFromBehavior = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>().SystemProperties.LockedUntilUtc;

                    return next();
                }
            }
        }

        public class Message : IMessage {}
    }
}