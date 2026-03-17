namespace NServiceBus.Transport.AzureServiceBus.Emulator.AcceptanceTests;

using NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

[RunOnlyWithEmulator]
public class When_publishing_a_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_it()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(
                (session, c) => session.Publish(new MyEvent())))
            .Run();

        Assert.That(context.EventReceived, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool EventReceived { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>(c => { }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));

        public class Handler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent request, IMessageHandlerContext context)
            {
                testContext.EventReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}