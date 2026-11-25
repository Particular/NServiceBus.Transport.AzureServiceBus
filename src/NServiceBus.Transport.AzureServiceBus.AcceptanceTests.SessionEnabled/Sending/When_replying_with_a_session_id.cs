namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.SessionEnabled.Sending;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_replying_with_a_session_id : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_in_that_session()
    {
        var requestSessionId = Guid.NewGuid().ToString();
        var replySessionId = Guid.NewGuid().ToString();

        var context = await Scenario.Define<Context>(c => c.ReplySessionId = replySessionId)
            .WithEndpoint<Sender>(b => b.When(session =>
            {
                var options = new SendOptions();
                options.SetSessionId(requestSessionId);
                return session.Send(new Request(), options);
            }))
            .WithEndpoint<Replier>()
            .Done(c => c.ReplyReceived)
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.RequestReceivedInSessionId, Is.EqualTo(requestSessionId));
            Assert.That(context.ReplyReceivedInSessionId, Is.EqualTo(replySessionId));
        }
    }

    public class Context : ScenarioContext
    {
        public string ReplySessionId { get; set; }
        public string RequestReceivedInSessionId { get; set; }
        public string ReplyReceivedInSessionId { get; set; }
        public bool ReplyReceived { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() => EndpointSetup<DefaultServer>(c => c.ConfigureRouting().RouteToEndpoint(typeof(Request), typeof(Replier)));

        [Handler]
        public class ReplyHandler(Context testContext) : IHandleMessages<Reply>
        {
            public Task Handle(Reply message, IMessageHandlerContext context)
            {
                testContext.ReplyReceivedInSessionId = context.Extensions.Get<IncomingMessage>().ReceiveProperties.GetValueOrDefault("SessionId");
                testContext.ReplyReceived = true;
                return Task.CompletedTask;
            }
        }
    }

    public class Replier : EndpointConfigurationBuilder
    {
        public Replier() => EndpointSetup<DefaultServer>();

        [Handler]
        public class RequestHandler(Context testContext) : IHandleMessages<Request>
        {
            public Task Handle(Request message, IMessageHandlerContext context)
            {
                testContext.RequestReceivedInSessionId = context.Extensions.Get<IncomingMessage>().ReceiveProperties.GetValueOrDefault("SessionId");

                var options = new ReplyOptions();
                options.SetSessionId(testContext.ReplySessionId);
                return context.Reply(new Reply(), options);
            }
        }
    }

    public class Request : ICommand;

    public class Reply : IMessage;
}
