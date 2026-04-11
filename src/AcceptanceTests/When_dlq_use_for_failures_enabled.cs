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

public class When_dlq_use_for_failures_enabled : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_dlq_failing_messages()
    {
        var errorSpyAddress = Conventions.EndpointNamingConvention(typeof(ErrorSpy));
        var context = await Scenario.Define<Context>()
            .WithEndpoint<UserEndpoint>(b => b
                .CustomConfig(c =>
                {
                    var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                    transport.DeadLetterFailedMessages = true;

                    // so that they gets forwarded to the central error queue
                    transport.AutoForwardDeadLetteredMessagesToErrorQueue = true;
                    c.SendFailedMessagesTo(errorSpyAddress);
                })
                .When(session => session.SendLocal(new FailingMessage()))
                .DoNotFailOnErrorMessages())
            .WithEndpoint<ErrorSpy>()
            .Run();

        var sourceEndpoint = Conventions.EndpointNamingConvention(typeof(UserEndpoint));
        var nativeMessage = context.ServiceBusReceivedMessage;
        var failedMessageHeaders = context.FailedMessageHeaders;
        Assert.Multiple(() =>
        {
            Assert.That(nativeMessage, Is.Not.Null);
            Assert.That(nativeMessage.DeadLetterSource, Is.EqualTo(sourceEndpoint.ToLower()), "Message should have come via the dlq of the processing endpoint");
            Assert.That(nativeMessage.DeadLetterReason, Is.EqualTo("NServiceBus"), "Reason should be NServiceBus");
            Assert.That(nativeMessage.DeadLetterErrorDescription, Is.EqualTo("See application properties"), "Message should indicate that failure details are in application properties");
            Assert.That(failedMessageHeaders[FaultsHeaderKeys.FailedQ], Is.EqualTo(sourceEndpoint), "Fault headers should be set");
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