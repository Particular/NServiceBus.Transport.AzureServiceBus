namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Azure.Messaging.ServiceBus;
using Faults;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_dlq_forwarding_is_enabled : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_forward_dlq_messages_to_error_queue()
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
        var nativeMessage = context.ServiceBusReceivedMessage;
        var failedMessageHeaders = context.FailedMessageHeaders;
        Assert.Multiple(() =>
        {
            Assert.That(nativeMessage, Is.Not.Null);
            Assert.That(nativeMessage.DeadLetterSource, Is.EqualTo(sourceEndpoint), "Message should have come via the dlq of the processing endpoint");

            // these should have been in a unit test but there is currently no way to create a mock service bus message with dlq reason and description set
            Assert.That(failedMessageHeaders[FaultsHeaderKeys.FailedQ], Is.EqualTo(nativeMessage.DeadLetterSource), $"{FaultsHeaderKeys.FailedQ} should be set to dlq source");
            Assert.That(failedMessageHeaders[FaultsHeaderKeys.Message].Contains("some message"), Is.True, $"{FaultsHeaderKeys.Message} should be set from dlq reason");
            Assert.That(failedMessageHeaders[FaultsHeaderKeys.ExceptionType], Is.EqualTo(typeof(SimulatedException).FullName), $"{FaultsHeaderKeys.ExceptionType} should be set if message was dead lettered via NServiceBus");
            Assert.That(failedMessageHeaders[FaultsHeaderKeys.StackTrace], Is.EqualTo(nativeMessage.DeadLetterErrorDescription), $"{FaultsHeaderKeys.StackTrace} should be set to dlq description");
        });
    }

    public class UserEndpoint : EndpointConfigurationBuilder
    {
        public UserEndpoint() => EndpointSetup<DefaultServer>();

        class Handler : IHandleMessages<FailingMessage>
        {
            public Task Handle(FailingMessage message, IMessageHandlerContext context) => throw new SimulatedException("some message");
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
                testContext.FailedMessageHeaders = context.MessageHeaders;
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