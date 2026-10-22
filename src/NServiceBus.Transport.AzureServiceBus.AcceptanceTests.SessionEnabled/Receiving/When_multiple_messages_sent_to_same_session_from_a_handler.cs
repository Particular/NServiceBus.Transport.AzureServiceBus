namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_multiple_messages_sent_to_same_session_from_a_handler : NServiceBusAcceptanceTest
{
    const int NumberOfMessages = 20;

    [Test]
    public async Task Should_process_messages_within_the_session_in_the_order_they_were_sent()
    {

        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When(async (session, _) =>
            {
                var options = new SendOptions();
                options.SetSessionId(Guid.NewGuid().ToString());
                options.RouteToThisEndpoint();

                await session.Send(new TriggerMessage(), options);
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

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>();

        [Handler]
        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.ReceivedSequences.Enqueue(message.Sequence);
                return Task.CompletedTask;
            }
        }

        [Handler]
        public class TriggerMessageHandler(Context testContext) : IHandleMessages<TriggerMessage>
        {
            public async Task Handle(TriggerMessage message, IMessageHandlerContext context)
            {
                var sessionId = Guid.NewGuid().ToString();

                for (var i = 0; i < NumberOfMessages; i++)
                {
                    var options = new SendOptions();
                    options.SetSessionId(sessionId);
                    options.RouteToThisEndpoint();
                    options.SetHeader("SequenceNumber", i.ToString());

                    // Awaiting each send in turn so messages are handed to the broker in this order.
                    await context.Send(new MyMessage { Sequence = i }, options);
                }
            }
        }
    }

    public class TriggerMessage : IMessage
    {
    }

    public class MyMessage : IMessage
    {
        public int Sequence { get; set; }
    }


}
