using System;
using System.Globalization;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_receiving_a_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_have_a_header_to_indicate_when_lock_will_be_lost()
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
                TestContext.LockedUntilUtcHeaderFound = DateTime.TryParseExact(context.MessageHeaders["ASB.System.LockedUntilUtc"], "yyyy-MM-dd HH:mm:ss:ffffff Z", null, DateTimeStyles.None, out _);

                return Task.FromResult(0);
            }
        }
    }

    public class Message : IMessage
    {
        public string Property { get; set; }
    }
}