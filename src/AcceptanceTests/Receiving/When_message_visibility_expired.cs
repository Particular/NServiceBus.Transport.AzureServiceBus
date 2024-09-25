namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_message_visibility_expired : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_complete_message_on_next_receive_when_pipeline_successful()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        // Limiting the concurrency for this test to make sure messages that are made available again are 
                        // not concurrently processed. This is not necessary for the test to pass but it makes
                        // reasoning about the test easier.
                        c.LimitMessageProcessingConcurrencyTo(1);
                    });
                    b.When((session, _) => session.SendLocal(new MyMessage()));
                })
                .Done(c => c.NativeMessageId != null && c.Logs.Any(l => WasMarkedAsSuccessfullyCompleted(l, c)))
                .Run();

            var items = ctx.Logs.Where(l => WasMarkedAsSuccessfullyCompleted(l, ctx)).ToArray();

            Assert.That(items, Is.Not.Empty);
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_when_error_pipeline_handled_the_message()
        {
            var ctx = await Scenario.Define<Context>(c =>
                {
                    c.ShouldThrow = true;
                })
                .WithEndpoint<Receiver>(b =>
                {
                    b.DoNotFailOnErrorMessages();
                    b.CustomConfig(c =>
                    {
                        var recoverability = c.Recoverability();
                        recoverability.AddUnrecoverableException<InvalidOperationException>();

                        // Limiting the concurrency for this test to make sure messages that are made available again are 
                        // not concurrently processed. This is not necessary for the test to pass but it makes
                        // reasoning about the test easier.
                        c.LimitMessageProcessingConcurrencyTo(1);
                    });
                    b.When((session, _) => session.SendLocal(new MyMessage()));
                })
                .Done(c => c.NativeMessageId != null && c.Logs.Any(l => WasMarkedAsSuccessfullyCompleted(l, c)))
                .Run();

            var items = ctx.Logs.Where(l => WasMarkedAsSuccessfullyCompleted(l, ctx)).ToArray();

            Assert.That(items, Is.Not.Empty);
        }

        static bool WasMarkedAsSuccessfullyCompleted(ScenarioContext.LogItem l, Context c)
            => l.Message.StartsWith($"Received message with id '{c.NativeMessageId}' was marked as successfully completed");

        class Context : ScenarioContext
        {
            public bool ShouldThrow { get; set; }

            public string NativeMessageId { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>(c =>
            {
                var transport = c.ConfigureTransport();
                // Explicitly setting the transport transaction mode to ReceiveOnly because the message 
                // tracking only is implemented for this mode.
                transport.Transactions(TransportTransactionMode.ReceiveOnly);
            });
        }

        public class MyMessage : IMessage
        {
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            readonly Context _testContext;

            public MyMessageHandler(Context testContext) => _testContext = testContext;

            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var messageReceiver = context.Extensions.Get<ServiceBusReceiver>();
                // By abandoning the message, the message will be "immediately available" for retrieval again and effectively the message pump
                // has lost the message visibility timeout because any Complete or Abandon will be rejected by the azure service bus.
                var serviceBusReceivedMessage = context.Extensions.Get<ServiceBusReceivedMessage>();
                await messageReceiver.AbandonMessageAsync(serviceBusReceivedMessage);

                _testContext.NativeMessageId = serviceBusReceivedMessage.MessageId;

                if (_testContext.ShouldThrow)
                {
                    throw new InvalidOperationException("Simulated exception");
                }
            }
        }
    }
}