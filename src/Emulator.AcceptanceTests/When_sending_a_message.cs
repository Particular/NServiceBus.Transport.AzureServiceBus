namespace NServiceBus.Transport.AzureServiceBus.Emulator.AcceptanceTests;

using NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[RunOnlyWithEmulator]
public class When_sending_a_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_it()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(
                (session, c) => session.SendLocal(new Message())))
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>();

        public class Handler(Context testContext) : IHandleMessages<Message>
        {
            public Task Handle(Message request, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : IMessage;
}