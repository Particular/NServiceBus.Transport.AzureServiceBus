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
            var context = await Scenario.Define<Context>()
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
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.MessageSessionSentMessageCustomizationReceived, Is.True, "Message sent via IMessageSession did not have the native message customization applied");
                Assert.That(context.MessageSessionPublishedMessageCustomizationReceived, Is.True, "Message published via IMessageSession did not have the native message customization applied");
                Assert.That(context.MessageHandlerContextSentMessageCustomizationReceived, Is.True, "Message sent via IMessageHandlerContext did not have the native message customization applied");
                Assert.That(context.MessageHandlerContextPublishedMessageCustomizationReceived, Is.True, "Message published via IMessageHandlerContext did not have the native message customization applied");
                Assert.That(context.PhysicalBehaviorMessageSentMessageCustomizationReceived, Is.True, "Message sent via physical behavior did not have the native message customization applied");
                Assert.That(context.LogicalBehaviorMessageSentMessageCustomizationReceived, Is.True, "Message sent via logical behavior did not have the native message customization applied");
            }
        }

        public class Context : ScenarioContext
        {
            public bool MessageSessionSentMessageCustomizationReceived { get; set; }
            public bool MessageSessionPublishedMessageCustomizationReceived { get; set; }
            public bool MessageHandlerContextSentMessageCustomizationReceived { get; set; }
            public bool PhysicalBehaviorMessageSentMessageCustomizationReceived { get; set; }
            public bool LogicalBehaviorMessageSentMessageCustomizationReceived { get; set; }
            public bool MessageHandlerContextPublishedMessageCustomizationReceived { get; set; }

            public void MaybeMarkAsCompleted() =>
                MarkAsCompleted(MessageSessionSentMessageCustomizationReceived
                                && MessageSessionPublishedMessageCustomizationReceived
                                && MessageHandlerContextSentMessageCustomizationReceived
                                && MessageHandlerContextPublishedMessageCustomizationReceived
                                && PhysicalBehaviorMessageSentMessageCustomizationReceived
                                && LogicalBehaviorMessageSentMessageCustomizationReceived);
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.Pipeline.Register(typeof(Handler.PhysicalBehavior), "Customizes a native message in a physical behavior");
                    c.Pipeline.Register(typeof(Handler.LogicalBehavior), "Customizes a native message in a logical behavior");
                }, metadata =>
                {
                    metadata.RegisterSelfAsPublisherFor<MessageSessionPublishedEvent>(this);
                    metadata.RegisterSelfAsPublisherFor<MessageHandlerContextPublishedEvent>(this);
                });

            public class Handler(Context testContext) :
                IHandleMessages<MessageSessionSentCommand>,
                IHandleMessages<PhysicalBehaviorSentCommand>,
                IHandleMessages<LogicalBehaviorSentCommand>,
                IHandleMessages<MessageSessionPublishedEvent>,
                IHandleMessages<MessageHandlerContextSentCommand>,
                IHandleMessages<MessageHandlerContextPublishedEvent>
            {
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

                public async Task Handle(MessageSessionSentCommand request, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.MessageSessionSentMessageCustomizationReceived = nativeMessage.Subject == "IMessageSession.Send";

                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.CustomizeNativeMessage(m => m.Subject = "IMessageHandlerContext.Send");

                    await context.Send(new MessageHandlerContextSentCommand(), sendOptions);
                }

                public async Task Handle(MessageSessionPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.MessageSessionPublishedMessageCustomizationReceived = nativeMessage.Subject == "IMessageSession.Publish";

                    var publishOptions = new PublishOptions();
                    publishOptions.CustomizeNativeMessage(m => m.Subject = "IMessageHandlerContext.Publish");

                    await context.Publish(new MessageHandlerContextPublishedEvent(), publishOptions);
                }

                public Task Handle(PhysicalBehaviorSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.PhysicalBehaviorMessageSentMessageCustomizationReceived = nativeMessage.Subject == "PhysicalBehavior.Send";
                    testContext.MaybeMarkAsCompleted();
                    return Task.CompletedTask;
                }

                public Task Handle(LogicalBehaviorSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.LogicalBehaviorMessageSentMessageCustomizationReceived = nativeMessage.Subject == "LogicalBehavior.Send";
                    testContext.MaybeMarkAsCompleted();
                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextSentCommand message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.MessageHandlerContextSentMessageCustomizationReceived = nativeMessage.Subject == "IMessageHandlerContext.Send";
                    testContext.MaybeMarkAsCompleted();
                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextPublishedEvent message, IMessageHandlerContext context)
                {
                    var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

                    testContext.MessageHandlerContextPublishedMessageCustomizationReceived = nativeMessage.Subject == "IMessageHandlerContext.Publish";
                    testContext.MaybeMarkAsCompleted();
                    return Task.CompletedTask;
                }
            }

            public class ValidateIncomingNativeMessages(Context testContext) : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    testContext.MessageSessionSentMessageCustomizationReceived = context.Extensions.Get<ServiceBusMessage>() != null;
                    testContext.MaybeMarkAsCompleted();

                    return next();
                }
            }
        }

        public class MessageSessionSentCommand : ICommand;
        public class PhysicalBehaviorSentCommand : ICommand;
        public class LogicalBehaviorSentCommand : ICommand;
        public class MessageSessionPublishedEvent : IEvent;
        public class MessageHandlerContextSentCommand : ICommand;
        public class MessageHandlerContextPublishedEvent : IEvent;
    }
}