namespace NServiceBus.AcceptanceTests.Routing;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using NUnit.Framework;

public class When_sessions_enabled_delayed_retry_tests : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_throw_exception_when_delayed_retries_are_enabled()
    {
        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await Scenario.Define<MyContext>()
                .WithEndpoint<EndpointWithDelayedRetries>()
                .Run());

        Assert.That(exception.ToString(), Does.Contain("Delayed retries are not supported for session-enabled receivers"));
    }

    [Test]
    public async Task Should_start_endpoint_when_delayed_retries_are_disabled()
    {
        var scenario = await Scenario.Define<MyContext>()
            .WithEndpoint<EndpointWithoutDelayedRetries>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
            .Run();

        Assert.That(scenario.MessageReceived, Is.True, "The message should be received");
    }

    [Test]
    public async Task Should_not_throw_exception_when_custom_policy_moves_message_to_error_queue()
    {
        var messageId = Guid.NewGuid().ToString();

        var exception = Assert.ThrowsAsync<MessageFailedException>(async () =>
            await Scenario.Define<MyContext>()
                .WithEndpoint<EndpointWithOnlyCustomPolicy>(b =>
                    b.When(bus =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.SetMessageId(messageId);
                        return bus.Send(new MyMessage(), sendOptions);
                    }))
                .Run());

        var context = (MyContext)exception.ScenarioContext;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.ToString(), Does.Not.Contain("Delayed retries are not supported for session-enabled receivers"));
            Assert.That(exception.FailedMessage.MessageId, Is.EqualTo(messageId));
        }
    }

    [Test]
    public async Task Should_throw_upon_partial_recoverability_customization()
    {
        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await Scenario.Define<MyContext>()
                .WithEndpoint<EndpointWithPartialRecoverabilityCustomization>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Run());

        Assert.That(exception.ToString(), Does.Contain("Delayed retries are not supported for session-enabled receivers"));
    }

    public class MyContext : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class EndpointWithDelayedRetries : EndpointConfigurationBuilder
    {
        public EndpointWithDelayedRetries() =>
            EndpointSetup<DefaultServer>(config =>
            {
                config.Recoverability().Delayed(delayed => delayed.NumberOfRetries(3));
            });
    }

    public class EndpointWithoutDelayedRetries : EndpointConfigurationBuilder
    {
        public EndpointWithoutDelayedRetries() =>
            EndpointSetup<DefaultServer>();

        [Handler]
        public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class EndpointWithOnlyCustomPolicy : EndpointConfigurationBuilder
    {
        public EndpointWithOnlyCustomPolicy() =>
            EndpointSetup<DefaultServer>((configure, context) =>
            {
                configure.Recoverability()
                    .CustomPolicy((cfg, errorContext) => RecoverabilityAction.MoveToError(cfg.Failed.ErrorQueue));
            });

        [Handler]
        public class MessageToBeRetriedHandler() : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                throw new SimulatedException();
            }
        }
    }

    public class EndpointWithPartialRecoverabilityCustomization : EndpointConfigurationBuilder
    {
        public EndpointWithPartialRecoverabilityCustomization() =>
            EndpointSetup<DefaultServer>((configure, context) =>
            {
                configure.Recoverability().AddUnrecoverableException<MyBusinessException>();
                configure.Recoverability().Immediate(immediate =>
                {
                    immediate.NumberOfRetries(3);
                });
                configure.Recoverability().Delayed(delayed =>
                {
                    delayed.NumberOfRetries(3).TimeIncrease(TimeSpan.FromSeconds(2));
                });
            });

        [Handler]
        public class MessageToBeRetriedHandler(MyContext testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : ICommand;

    public class MyBusinessException : Exception;
}