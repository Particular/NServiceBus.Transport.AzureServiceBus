namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Azure.Messaging.ServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_dlq_forwarding_is_enabled : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_be_able_to_read_dlq_messages_from_error_queue()
    {
        var errorSpyAddress = Conventions.EndpointNamingConvention(typeof(ErrorSpy));
        var context = await Scenario.Define<Context>()
            .WithEndpoint<UserEndpoint>(b => b
                .CustomConfig(c =>
                {
                    c.ConfigureTransport<AzureServiceBusTransport>().AutoForwardDeadLetteredMessagesToErrorQueue = true;
                    c.SendFailedMessagesTo(errorSpyAddress);
                    c.Recoverability().CustomPolicy((_, errorContext) => RecoverabilityAction.DeadLetter(errorContext.Exception));
                })
                .When(session => session.SendLocal(new FailingMessage()))
                .DoNotFailOnErrorMessages())
            .WithEndpoint<ErrorSpy>()
            .Run();

        var sourceEndpoint = Conventions.EndpointNamingConvention(typeof(UserEndpoint)).ToLower();
        Assert.Multiple(() =>
        {
            Assert.That(context.ServiceBusReceivedMessage, Is.Not.Null);
            Assert.That(context.ServiceBusReceivedMessage.DeadLetterSource, Is.EqualTo(sourceEndpoint), "Message should have come via the dlq of the processing endpoint");
        });
    }

    public class UserEndpoint : EndpointConfigurationBuilder
    {
        public UserEndpoint() => EndpointSetup<DefaultServer>();

        class Handler : IHandleMessages<FailingMessage>
        {
            public Task Handle(FailingMessage message, IMessageHandlerContext context) => throw new SimulatedException();
        }
    }

    public class ErrorSpy : EndpointConfigurationBuilder
    {
        public ErrorSpy() =>
            EndpointSetup<DefaultServer>();

        class Handler(Context testContext) : IHandleMessages<FailingMessage>
        {
            public Task Handle(FailingMessage message, IMessageHandlerContext context)
            {
                testContext.ServiceBusReceivedMessage = context.Extensions.Get<ServiceBusReceivedMessage>();
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Context : ScenarioContext
    {
        public IReadOnlyDictionary<string, string> FailedMessageHeaders { get; set; }
        public ServiceBusReceivedMessage ServiceBusReceivedMessage { get; set; }
    }

    public class FailingMessage : IMessage;
}