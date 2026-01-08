namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests.Sending;

using System.Threading.Tasks;
using AcceptanceTesting;
using Azure.Messaging.ServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

class When_customizing_outgoing_native_messages : NServiceBusAcceptanceTest
{
    const string TestSubject = "0192c3ad-8ab2-77a0-8a92-2be53f062e06";

    [Test]
    public async Task Should_dispatch_native_message_with_the_customizations()
    {
        var scenario = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new Message())))
            .Run();

        Assert.That(scenario.ReceivedMessage.Subject, Is.EqualTo(TestSubject));
    }

    class Context : ScenarioContext
    {
        public ServiceBusReceivedMessage ReceivedMessage { get; set; }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() =>
            EndpointSetup<DefaultServer>(endpointConfiguration =>
            {
                var transport = endpointConfiguration.ConfigureTransport<AzureServiceBusTransport>();
                transport.OutgoingNativeMessageCustomization = (_, message) => message.Subject = TestSubject;
            });

        class MyEventHandler(Context testContext) : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context)
            {
                testContext.ReceivedMessage = context.Extensions.Get<ServiceBusReceivedMessage>();
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : IMessage;
}