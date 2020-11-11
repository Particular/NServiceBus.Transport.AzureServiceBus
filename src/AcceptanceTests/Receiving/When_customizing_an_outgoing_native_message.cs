namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending.Receiving
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_customizing_an_outgoing_native_message
    {
        [Test]
        public async Task Should_dispatch_native_message_with_the_customizations()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(async (session, c) =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.CustomizeNativeMessage(m => m.Label = "IMessageSession.Send");
                        await session.Send(new MessageSessionSentCommand(), sendOptions);

                        var publishOptions = new PublishOptions();
                        publishOptions.CustomizeNativeMessage(m => m.Label = "IMessageSession.Publish");
                        await session.Publish(new MessageSessionPublishedEvent(), publishOptions);
                    }))
                .Done(c => c.Completed)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool MessageSessionSentMessageCustomizationReceived { get; set; }
            public bool MessageSessionPublishedMessageCustomizationReceived { get; set; }
            public bool MessageHandlerContextSentMessageCustomizationReceived { get; set; }
            public bool MessageHandlerContextPublishedMessageCustomizationReceived { get; set; }

            public bool Completed => MessageSessionSentMessageCustomizationReceived
                                     && MessageSessionPublishedMessageCustomizationReceived
                                     && MessageHandlerContextSentMessageCustomizationReceived
                                     && MessageHandlerContextPublishedMessageCustomizationReceived;
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
                // EndpointSetup<DefaultServer>((c, d) =>
                //     c.Pipeline.Register(b => new ValidateIncomingNativeMessages((Context)d.ScenarioContext), "Behavior to validate the native messages contain customizations assigned when those native messages were dispatched"));
            }

            public class Handler :
                IHandleMessages<MessageSessionSentCommand>,
                IHandleMessages<MessageSessionPublishedEvent>,
                IHandleMessages<MessageHandlerContextSentCommand>,
                IHandleMessages<MessageHandlerContextPublishedEvent>
            {
                Context testContext;

                public Handler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MessageSessionSentCommand request, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>();

                    testContext.MessageSessionSentMessageCustomizationReceived = nativeMessage.Label == "IMessageSession.Send";

                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.CustomizeNativeMessage(m => m.Label = "IMessageHandlerContext.Send");

                    return context.Send(new MessageHandlerContextSentCommand(), sendOptions);
                }

                public Task Handle(MessageSessionPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>();

                    testContext.MessageSessionPublishedMessageCustomizationReceived = nativeMessage.Label == "IMessageSession.Publish";

                    var publishOptions = new PublishOptions();
                    publishOptions.CustomizeNativeMessage(m => m.Label = "IMessageHandlerContext.Publish");

                    return context.Publish(new MessageHandlerContextPublishedEvent(), publishOptions);
                }

                public Task Handle(MessageHandlerContextSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>();

                    testContext.MessageHandlerContextSentMessageCustomizationReceived = nativeMessage.Label == "IMessageHandlerContext.Send";

                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>();

                    testContext.MessageHandlerContextPublishedMessageCustomizationReceived = nativeMessage.Label == "IMessageHandlerContext.Publish";

                    return Task.CompletedTask;
                }
            }

            public class ValidateIncomingNativeMessages : Behavior<ITransportReceiveContext>
            {
                readonly Context testContext;

                public ValidateIncomingNativeMessages(Context context)
                {
                    testContext = context;
                }

                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    testContext.MessageSessionSentMessageCustomizationReceived = context.Extensions.Get<Microsoft.Azure.ServiceBus.Message>() != null;

                    return next();
                }
            }
        }

        public class MessageSessionSentCommand : ICommand {}
        public class MessageSessionPublishedEvent : IEvent {}
        public class MessageHandlerContextSentCommand : ICommand { }
        public class MessageHandlerContextPublishedEvent : IEvent { }
    }
}