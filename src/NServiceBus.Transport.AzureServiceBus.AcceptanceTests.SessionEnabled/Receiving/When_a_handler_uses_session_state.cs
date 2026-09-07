namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_a_handler_uses_session_state : NServiceBusAcceptanceTest
{
    const int NumberOfMessages = 10;

    [Test]
    public async Task Should_round_trip_state_through_the_service_across_messages_in_a_session()
    {
        string sessionId = Guid.NewGuid().ToString();

        Context ctx = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When(async (session, _) =>
            {
                for (int i = 1; i <= NumberOfMessages; i++)
                {
                    var options = new SendOptions();
                    options.SetSessionId(sessionId);
                    options.RouteToThisEndpoint();

                    await session.Send(new Tick { Value = i }, options);
                }
            }))
            .Done(c => c.NrOfMessagesProcessed == NumberOfMessages)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(ctx.NrOfMessagesProcessed, Is.EqualTo(NumberOfMessages));
            Assert.That(ctx.CalculatedSum, Is.EqualTo(ctx.StoredSum));
        });
    }

    [Test]
    public async Task Should_not_persist_the_session_state_written_by_the_failed_attempt()
    {
        string sessionId = Guid.NewGuid().ToString();

        Context ctx = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b =>
            {
                b.CustomConfig(c => c.Recoverability()
                    .Immediate(i => i.NumberOfRetries(3)));
                b.When(async (session, _) =>
                {
                    for (int i = 1; i <= NumberOfMessages; i++)
                    {
                        var options = new SendOptions();
                        options.SetSessionId(sessionId);
                        options.RouteToThisEndpoint();

                        await session.Send(new Tick { Value = i, InjectFailure = i == 5 }, options);
                    }
                });
            })
            .Done(c => c.NrOfMessagesProcessed == NumberOfMessages)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(ctx.FailureInjected, Is.True, "No failure was induced");
            Assert.That(ctx.StoredSum, Is.EqualTo(ctx.CalculatedSum));
        });
    }

    public class Context : ScenarioContext
    {
        public int NrOfMessagesProcessed { get; set; }
        public int CalculatedSum { get; set; }
        public int StoredSum { get; set; }
        public bool FailureInjected { get; set; }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>(config =>
            config.Recoverability().Delayed(settings => settings.NumberOfRetries(0)));
    }

    public class Tick : IMessage
    {
        public int Value { get; set; }
        public bool InjectFailure { get; set; }
    }

    public class CounterState
    {
        public int Sum { get; set; }
    }

    [Handler]
    public class TickHandler(Context testContext) : IHandleMessages<Tick>
    {
        public async Task Handle(Tick message, IMessageHandlerContext context)
        {
            testContext.NrOfMessagesProcessed++;
            IAzureServiceBusSessionState sessionState = context.Extensions.Get<IAzureServiceBusSessionState>();

            // Evolve state based on what's already there
            CounterState state = await sessionState.Get<CounterState>(context.CancellationToken) ?? new CounterState();

            state.Sum += message.Value;

            await sessionState.Set(state, context.CancellationToken);

            // Fail once if instructed
            if (message.InjectFailure && !testContext.FailureInjected)
            {
                testContext.FailureInjected = true;
                throw new SimulatedException($"Induced failure after writing session state for value {message.Value}");
            }

            testContext.CalculatedSum += message.Value;
            testContext.StoredSum = state.Sum;
        }
    }
}