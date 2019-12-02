namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending.Receiving
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_a_message
    {
        [Test]
        public async Task Should_have_lock_expiration_in_the_context_bag()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(
                    (session, c) => session.SendLocal(new Message
                    {
                        Property = "value"
                    })))
                .Done(c => c.LockedUntilUtcHeaderFound)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool LockedUntilUtcHeaderFound { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class Handler : IHandleMessages<Message>
            {
                public Context TestContext { get; set; }

                public Task Handle(Message request, IMessageHandlerContext context)
                {
                    TestContext.LockedUntilUtcHeaderFound = context.Extensions.TryGet<DateTime>("Message.SystemProperties.LockedUntilUtc", out _);

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : IMessage
        {
            public string Property { get; set; }
        }
    }
}