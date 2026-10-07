namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using Azure.Messaging.ServiceBus;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

/// <summary>
/// TransportTransactionMode.None is not included here as it's not supported for sessions
/// </summary>
public class When_a_handler_throws_after_setting_session_state : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Rolls_back_the_change_set_before_the_throw_with_SendsAtomicWithReceive()
    {
        var persisted = await RunScenarioAndGetPersistedSum(TransportTransactionMode.SendsAtomicWithReceive);

        Assert.That(persisted, Is.EqualTo(10),
            "The change made before the exception should enlist in the same transaction as the rest of the processing" +
            "- so it should have rolled back and not be durable in the session state.");
    }

    [Test]
    public async Task Keeps_the_change_set_before_the_throw_with_ReceiveOnly()
    {
        var persisted = await RunScenarioAndGetPersistedSum(TransportTransactionMode.ReceiveOnly);

        Assert.That(persisted, Is.EqualTo(15),
            "ReceiveOnly has no transaction for the change to roll back with, so it should be persisted " +
            "even though the handler that made it went on to throw.");
    }

    static async Task<int?> RunScenarioAndGetPersistedSum(TransportTransactionMode transactionMode)
    {
        string sessionId = Guid.NewGuid().ToString();

        Context ctx = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b =>
            {
                b.DoNotFailOnErrorMessages();
                b.CustomConfig(c =>
                {
                    c.Recoverability().Immediate(i => i.NumberOfRetries(0));
                    c.Recoverability().Delayed(d => d.NumberOfRetries(0));

                    c.ConfigureTransport<AzureServiceBusTransport>().TransportTransactionMode = transactionMode;
                });
                b.When(async (session, _) =>
                {
                    var baselineOptions = new SendOptions();
                    baselineOptions.SetSessionId(sessionId);
                    baselineOptions.RouteToThisEndpoint();
                    await session.Send(new Tick { Value = 10, InjectFailure = false }, baselineOptions);
                });
                b.When(c => c.StoredSum.HasValue, async (session, _) =>
                {
                    var failingOptions = new SendOptions();
                    failingOptions.SetSessionId(sessionId);
                    failingOptions.RouteToThisEndpoint();
                    await session.Send(new Tick { Value = 5, InjectFailure = true }, failingOptions);
                });
            })
            .Done(c => c.StoredSum.HasValue && c.FailedMessages.SelectMany(kvp => kvp.Value).Any())
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(ctx.StoredSum, Is.EqualTo(10), "The baseline Tick should have established Sum = 10.");
            Assert.That(ctx.FailedMessages.SelectMany(kvp => kvp.Value), Has.Exactly(1).Items,
                "The second Tick should have failed exactly once and landed in the error queue, with no retries.");
        });

        return await ReadPersistedSum(sessionId);
    }

    static async Task<int?> ReadPersistedSum(string sessionId)
    {
        var queueName = Conventions.EndpointNamingConvention(typeof(Receiver));

        await using var client = new ServiceBusClient(AcceptanceTestConnectionString.Get());
        ServiceBusSessionReceiver receiver = await client.AcceptSessionAsync(queueName, sessionId);

        try
        {
            BinaryData raw = await receiver.GetSessionStateAsync();

            if (raw is null || raw.ToMemory().IsEmpty)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(raw.ToMemory());

            if (!doc.RootElement.TryGetProperty("user", out var user))
            {
                return null;
            }

            return user.GetProperty("data").GetProperty("sum").GetInt32();
        }
        finally
        {
            await receiver.CloseAsync();
        }
    }

    public class Context : ScenarioContext
    {
        public int? StoredSum { get; set; }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>();
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
            IAzureServiceBusSessionState sessionState = context.GetSessionState();
            CounterState state = await sessionState.Get<CounterState>(context.CancellationToken) ?? new CounterState();
            state.Sum += message.Value;
            await sessionState.Set(state, context.CancellationToken);

            if (message.InjectFailure)
            {
                throw new SimulatedException("Induced failure after writing session state");
            }

            testContext.StoredSum = state.Sum;
        }
    }
}
