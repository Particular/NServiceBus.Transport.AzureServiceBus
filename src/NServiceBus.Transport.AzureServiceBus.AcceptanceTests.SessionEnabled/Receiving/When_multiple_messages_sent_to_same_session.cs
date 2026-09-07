namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_multiple_messages_sent_to_same_session : NServiceBusAcceptanceTest
{
    const int NumberOfMessages = 20;

    [Test]
    public async Task Should_process_messages_within_the_session_in_the_order_they_were_sent()
    {
        var sessionId = Guid.NewGuid().ToString();

        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When(async (session, _) =>
            {
                for (var i = 0; i < NumberOfMessages; i++)
                {
                    var options = new SendOptions();
                    options.SetSessionId(sessionId);
                    options.RouteToThisEndpoint();

                    // Awaiting each send in turn so messages are handed to the broker in this order.
                    await session.Send(new MyMessage { Sequence = i }, options);
                }
            }))
            .Done(c => c.ReceivedSequences.Count == NumberOfMessages)
            .Run();

        Assert.That(ctx.ReceivedSequences, Is.EqualTo(Enumerable.Range(0, NumberOfMessages)),
            "Messages belonging to the same session should be processed in the order they were sent.");
    }

    public class Context : ScenarioContext
    {
        public ConcurrentQueue<int> ReceivedSequences { get; } = new();
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>();
    }

    public class MyMessage : IMessage
    {
        public int Sequence { get; set; }
    }

    [Handler]
    public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            testContext.ReceivedSequences.Enqueue(message.Sequence);
            return Task.CompletedTask;
        }
    }
}
