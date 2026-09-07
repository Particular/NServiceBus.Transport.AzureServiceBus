namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using Azure.Messaging.ServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_saga_processes_multiple_messages_in_the_same_session : NServiceBusAcceptanceTest
{
    // The operations that make up the basket's activity in the order they are sent.
    static readonly List<(bool IsAdd, string Product)> BasketOperations =
    [
        (IsAdd: true, Product: "Dates"),
        (IsAdd: true, Product: "Apples"),
        (IsAdd: true, Product: "Bananas"),
        (IsAdd: false, Product: "Dates"),
        (IsAdd: true, Product: "Eggplant"),
        (IsAdd: false, Product: "Bananas"),
        (IsAdd: true, Product: "Dates"),
        (IsAdd: true, Product: "Cherries"),
    ];

    // The net effect of all the operations above
    static readonly string[] ExpectedFinalProducts = ["Dates", "Apples", "Eggplant", "Cherries"];

    [Test]
    public async Task Should_correlate_all_saga_messages_to_the_same_session()
    {
        var orderId = Guid.NewGuid();
        var sessionId = orderId.ToString();

        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(async (session, _) =>
            {
                foreach (var (isAdd, product) in BasketOperations)
                {
                    var options = new SendOptions();
                    options.SetSessionId(sessionId);
                    options.RouteToThisEndpoint();

                    object message = isAdd
                        ? new AddProductToBasket { OrderId = orderId, Product = product }
                        : new RemoveProductFromBasket { OrderId = orderId, Product = product };

                    await session.Send(message, options);
                }

                var checkoutOptions = new SendOptions();
                checkoutOptions.SetSessionId(sessionId);
                checkoutOptions.RouteToThisEndpoint();

                await session.Send(new Checkout { OrderId = orderId }, checkoutOptions);
            }))
            .Done(c => c.SagaCompleted)
            .Run();

        var expectedMessageCount = BasketOperations.Count + 1; // + 1 = Checkout
        Assert.Multiple(() =>
        {
            Assert.That(ctx.SessionIds, Has.Count.EqualTo(expectedMessageCount),
                "Every add/remove/checkout message should have been processed.");
            Assert.That(ctx.SessionIds, Is.All.EqualTo(sessionId),
                "All messages that were processed by the saga should have the same session id.");
            Assert.That(ctx.FinalProducts.Order(), Is.EqualTo(ExpectedFinalProducts.Order()),
                "The saga should reflect the net effect of all add/remove messages in the order they were sent in.");
        });
    }

    [Test]
    public async Task Should_not_break_message_order_when_a_handler_fails_once()
    {
        var orderId = Guid.NewGuid();
        var sessionId = orderId.ToString();

        var failingStep = new Random().Next(BasketOperations.Count);

        var ctx = await Scenario.Define<Context>(c => c.FailingStep = failingStep)
            .WithEndpoint<Endpoint>(b => b.When(async (session, _) =>
            {
                for (var step = 0; step < BasketOperations.Count; step++)
                {
                    var (isAdd, product) = BasketOperations[step];

                    var options = new SendOptions();
                    options.SetSessionId(sessionId);
                    options.RouteToThisEndpoint();

                    object message = isAdd
                        ? new AddProductToBasket { OrderId = orderId, Product = product, Step = step }
                        : new RemoveProductFromBasket { OrderId = orderId, Product = product, Step = step };

                    await session.Send(message, options);
                }

                var checkoutOptions = new SendOptions();
                checkoutOptions.SetSessionId(sessionId);
                checkoutOptions.RouteToThisEndpoint();

                await session.Send(new Checkout { OrderId = orderId, Step = BasketOperations.Count }, checkoutOptions);
            }))
            .Done(c => c.SagaCompleted)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(ctx.ProcessedSteps, Is.EqualTo(Enumerable.Range(0, BasketOperations.Count + 1)),
                $"Due to an induced failure, the messages in the session were not processed in order.");
            Assert.That(ctx.FinalProducts.Order(), Is.EqualTo(ExpectedFinalProducts.Order()),
                "The saga should reflect the net effect of all add/remove messages even though one of them failed and was retried.");
        });
    }

    public class Context : ScenarioContext
    {
        public int FailingStep { get; set; } = -1; // -1 will never fail
        public ConcurrentDictionary<int, bool> FailureInduced { get; } = new();
        public ConcurrentQueue<string> SessionIds { get; } = new();
        public ConcurrentQueue<int> ProcessedSteps { get; } = new();
        public List<string> FinalProducts { get; set; } = [];
        public bool SagaCompleted { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>(c =>
        {
            c.Recoverability()
                .Delayed(d => d.NumberOfRetries(0))
                .Immediate(i => i.NumberOfRetries(3));
        });

        [Saga]
        public class OrderSaga(Context testContext) : Saga<OrderSaga.OrderSagaData>,
            IAmStartedByMessages<AddProductToBasket>,
            IHandleMessages<RemoveProductFromBasket>,
            IHandleMessages<Checkout>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderSagaData> mapper)
            {
                mapper.MapSaga(saga => saga.OrderId).ToMessage<AddProductToBasket>(m => m.OrderId);
                mapper.MapSaga(saga => saga.OrderId).ToMessage<RemoveProductFromBasket>(m => m.OrderId);
                mapper.MapSaga(saga => saga.OrderId).ToMessage<Checkout>(m => m.OrderId);
            }

            public Task Handle(AddProductToBasket message, IMessageHandlerContext context)
            {
                FailStepIfNeeded(message.Step);
                Data.OrderId = message.OrderId;
                Data.Products.Add(message.Product);
                RecordProcessedStep(context, message.Step);
                return Task.CompletedTask;
            }

            public Task Handle(RemoveProductFromBasket message, IMessageHandlerContext context)
            {
                FailStepIfNeeded(message.Step);
                Data.Products.Remove(message.Product);
                RecordProcessedStep(context, message.Step);
                return Task.CompletedTask;
            }

            public Task Handle(Checkout message, IMessageHandlerContext context)
            {
                RecordProcessedStep(context, message.Step);
                testContext.FinalProducts = [.. Data.Products];
                testContext.SagaCompleted = true;
                MarkAsComplete();
                return Task.CompletedTask;
            }

            void FailStepIfNeeded(int step)
            {
                if (step == testContext.FailingStep && testContext.FailureInduced.TryAdd(step, true))
                {
                    throw new SimulatedException($"Simulated failure for step {step}");
                }
            }

            void RecordProcessedStep(IMessageHandlerContext context, int step)
            {
                var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();
                testContext.SessionIds.Enqueue(nativeMessage.SessionId);
                testContext.ProcessedSteps.Enqueue(step);
            }

            public class OrderSagaData : ContainSagaData
            {
                public virtual Guid OrderId { get; set; }
                public virtual List<string> Products { get; set; } = [];
            }
        }
    }

    public class AddProductToBasket : IMessage
    {
        public Guid OrderId { get; set; }
        public string Product { get; set; }
        public int Step { get; set; }
    }

    public class RemoveProductFromBasket : IMessage
    {
        public Guid OrderId { get; set; }
        public string Product { get; set; }
        public int Step { get; set; }
    }

    public class Checkout : IMessage
    {
        public Guid OrderId { get; set; }
        public int Step { get; set; }
    }
}
