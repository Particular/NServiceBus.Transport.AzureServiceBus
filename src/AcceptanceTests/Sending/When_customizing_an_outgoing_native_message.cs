namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_customizing_an_outgoing_native_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_dispatch_native_message_with_the_customizations()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(async (session, c) =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.CustomizeNativeMessage(m => m.Subject = "IMessageSession.Send");
                        await session.Send(new MessageSessionSentCommand(), sendOptions);

                        var publishOptions = new PublishOptions();
                        publishOptions.CustomizeNativeMessage(m => m.Subject = "IMessageSession.Publish");
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
            public bool PhysicalBehaviorMessageSentMessageCustomizationReceived { get; set; }
            public bool LogicalBehaviorMessageSentMessageCustomizationReceived { get; set; }
            public bool MessageHandlerContextPublishedMessageCustomizationReceived { get; set; }

            public bool Completed => MessageSessionSentMessageCustomizationReceived
                                     && MessageSessionPublishedMessageCustomizationReceived
                                     && MessageHandlerContextSentMessageCustomizationReceived
                                     && MessageHandlerContextPublishedMessageCustomizationReceived
                                     && PhysicalBehaviorMessageSentMessageCustomizationReceived
                                     && LogicalBehaviorMessageSentMessageCustomizationReceived;
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.Pipeline.Register(typeof(Handler.PhysicalBehavior), "Customizes a native message in a physical behavior");
                    c.Pipeline.Register(typeof(Handler.LogicalBehavior), "Customizes a native message in a logical behavior");
                });
            }

            public class Handler :
                IHandleMessages<MessageSessionSentCommand>,
                IHandleMessages<PhysicalBehaviorSentCommand>,
                IHandleMessages<LogicalBehaviorSentCommand>,
                IHandleMessages<MessageSessionPublishedEvent>,
                IHandleMessages<MessageHandlerContextSentCommand>,
                IHandleMessages<MessageHandlerContextPublishedEvent>
            {
                Context testContext;

                public Handler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public class PhysicalBehavior : Behavior<IIncomingPhysicalMessageContext>
                {
                    public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.CustomizeNativeMessage(m => m.Subject = "PhysicalBehavior.Send");

                        await context.Send(new PhysicalBehaviorSentCommand(), sendOptions);

                        await next();
                    }
                }

                public class LogicalBehavior : Behavior<IIncomingLogicalMessageContext>
                {
                    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.CustomizeNativeMessage(m => m.Subject = "LogicalBehavior.Send");

                        await context.Send(new LogicalBehaviorSentCommand(), sendOptions);

                        await next();
                    }
                }

                public Task Handle(MessageSessionSentCommand request, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.MessageSessionSentMessageCustomizationReceived = nativeMessage.Subject == "IMessageSession.Send";

                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.CustomizeNativeMessage(m => m.Subject = "IMessageHandlerContext.Send");

                    return context.Send(new MessageHandlerContextSentCommand(), sendOptions);
                }

                public Task Handle(MessageSessionPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.MessageSessionPublishedMessageCustomizationReceived = nativeMessage.Subject == "IMessageSession.Publish";

                    var publishOptions = new PublishOptions();
                    publishOptions.CustomizeNativeMessage(m => m.Subject = "IMessageHandlerContext.Publish");

                    return context.Publish(new MessageHandlerContextPublishedEvent(), publishOptions);
                }

                public Task Handle(PhysicalBehaviorSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.PhysicalBehaviorMessageSentMessageCustomizationReceived = nativeMessage.Subject == "PhysicalBehavior.Send";

                    return Task.CompletedTask;
                }

                public Task Handle(LogicalBehaviorSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.LogicalBehaviorMessageSentMessageCustomizationReceived = nativeMessage.Subject == "LogicalBehavior.Send";

                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.MessageHandlerContextSentMessageCustomizationReceived = nativeMessage.Subject == "IMessageHandlerContext.Send";

                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusMessage>();

                    testContext.MessageHandlerContextPublishedMessageCustomizationReceived = nativeMessage.Subject == "IMessageHandlerContext.Publish";

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
                    testContext.MessageSessionSentMessageCustomizationReceived = context.Extensions.Get<ServiceBusMessage>() != null;

                    return next();
                }
            }
        }

        public class MessageSessionSentCommand : ICommand { }
        public class PhysicalBehaviorSentCommand : ICommand { }
        public class LogicalBehaviorSentCommand : ICommand { }
        public class MessageSessionPublishedEvent : IEvent { }
        public class MessageHandlerContextSentCommand : ICommand { }
        public class MessageHandlerContextPublishedEvent : IEvent { }
    }
}