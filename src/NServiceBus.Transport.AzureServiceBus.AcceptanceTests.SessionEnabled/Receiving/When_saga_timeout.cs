namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.SessionEnabled.Receiving;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_saga_timeout : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_attach_incoming_session_id_to_timeout_message()
    {
        var messageId = Guid.NewGuid();
        var sessionId = messageId.ToString();

        var testContext = await Scenario.Define<TestContext>()
            .WithEndpoint<Endpoint>(b => b.When(async session =>
            {
                var options = new SendOptions();
                options.SetSessionId(sessionId);
                options.RouteToThisEndpoint();

                await session.Send(new StartSagaCommand { DataId = messageId }, options);
            }))
            .Done(c => c.SagaCompleted)
            .Run();


        Assert.That(testContext.TimeOutSessionId, Is.EqualTo(sessionId), "Session id matches");
    }

    public class TestContext : ScenarioContext
    {
       public bool SagaCompleted { get; set; }
        public string TimeOutSessionId { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>();

        [Saga]
        public class MySaga(TestContext testContext) : Saga<MySaga.MySagaData>,
            IAmStartedByMessages<StartSagaCommand>,
            IHandleMessages<AnotherMessage>,
            IHandleTimeouts<MySagaTimeout>
        {
            public Task Handle(StartSagaCommand message, IMessageHandlerContext context)
            {
                Data.DataId = message.DataId;
                return RequestTimeout(context, TimeSpan.FromSeconds(1), new MySagaTimeout());
            }

            public async Task Timeout(MySagaTimeout state, IMessageHandlerContext context)
            {
                var incomingMessage = context.Extensions.Get<IncomingMessage>();
                var sessionId = incomingMessage.ReceiveProperties.GetValueOrDefault("SessionId");
                testContext.TimeOutSessionId = sessionId;
                await context.SendLocal(new AnotherMessage { DataId = Data.DataId });
            }

            public Task Handle(AnotherMessage message, IMessageHandlerContext context)
            {
                testContext.SagaCompleted = true;
                MarkAsComplete();
                return Task.CompletedTask;
            }

            //session id and message correlation are the same
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) =>
                mapper.MapSaga(s => s.DataId)
                    .ToMessage<StartSagaCommand>(m => m.DataId)
                    .ToMessage<AnotherMessage>(m => m.DataId);

            public class MySagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }
    }

    public class StartSagaCommand : ICommand
    {
        public Guid DataId { get; set; }
    }

    public class AnotherMessage : ICommand
    {
        public Guid DataId { get; set; }
    }

    public class MySagaTimeout : IMessage;
}