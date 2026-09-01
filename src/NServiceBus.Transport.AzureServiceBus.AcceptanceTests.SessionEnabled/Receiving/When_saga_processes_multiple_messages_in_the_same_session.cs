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
    static readonly string[] AllProducts = ["Apples", "Bananas", "Cherries", "Dates", "Eggplant"];
    static readonly string[] ProductsToRemove = ["Bananas", "Dates"];

    [Test]
    public async Task Should_correlate_all_saga_messages_to_the_same_session()
    {
        var orderId = Guid.NewGuid();
        var sessionId = orderId.ToString();
        var operations = BuildShuffledOperations(new Random());

        var ctx = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(async (session, _) =>
            {
                foreach (var (isAdd, product) in operations)
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

        var expectedProducts = AllProducts.Except(ProductsToRemove).Order();
        var expectedMessageCount = operations.Count + 1; // + Checkout

        Assert.Multiple(() =>
        {
            Assert.That(ctx.SessionIds, Has.Count.EqualTo(expectedMessageCount),
                "Every add/remove/checkout message should have been processed.");
            Assert.That(ctx.SessionIds, Is.All.EqualTo(sessionId),
                "All messages belonging that were processed in the saga should have the same session id.");
            Assert.That(ctx.FinalProducts.Order(), Is.EqualTo(expectedProducts),
                "The saga should reflect the net effect of all add/remove messages in the order they arrived in.");
        });
    }

    static List<(bool IsAdd, string Product)> BuildShuffledOperations(Random random)
    {
        // First do the adds, so we don't remove anything we didn't add first
        var pending = AllProducts.Select(p => (IsAdd: true, Product: p)).ToList();
        var scheduled = new List<(bool IsAdd, string Product)>();

        while (pending.Count > 0)
        {
            var index = random.Next(pending.Count);
            var operation = pending[index];
            pending.RemoveAt(index);
            scheduled.Add(operation);

            if (operation.IsAdd && ProductsToRemove.Contains(operation.Product))
            {
                pending.Add(operation with { IsAdd = false });
            }
        }

        return scheduled;
    }

    public class Context : ScenarioContext
    {
        public ConcurrentQueue<string> SessionIds { get; } = new();
        public List<string> FinalProducts { get; set; } = [];
        public bool SagaCompleted { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>();

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
                Data.OrderId = message.OrderId;
                Data.Products.Add(message.Product);
                RecordSessionId(context);
                return Task.CompletedTask;
            }

            public Task Handle(RemoveProductFromBasket message, IMessageHandlerContext context)
            {
                Data.Products.Remove(message.Product);
                RecordSessionId(context);
                return Task.CompletedTask;
            }

            public Task Handle(Checkout message, IMessageHandlerContext context)
            {
                RecordSessionId(context);
                testContext.FinalProducts = [.. Data.Products];
                testContext.SagaCompleted = true;
                MarkAsComplete();
                return Task.CompletedTask;
            }

            void RecordSessionId(IMessageHandlerContext context)
            {
                var nativeMessage = context.Extensions.Get<ServiceBusReceivedMessage>();
                testContext.SessionIds.Enqueue(nativeMessage.SessionId);
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
    }

    public class RemoveProductFromBasket : IMessage
    {
        public Guid OrderId { get; set; }
        public string Product { get; set; }
    }

    public class Checkout : IMessage
    {
        public Guid OrderId { get; set; }
    }
}
