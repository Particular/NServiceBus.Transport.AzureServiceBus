﻿using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Features;
using NUnit.Framework;

class When_sending_message_outside_of_a_handler_with_incorrect_transaction_scope : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_dispatch_message()
    {
        await Scenario.Define<Context>()
            .WithEndpoint<Receiver>()
            .Done(context => context.Received)
            .Run();
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(c => c.EnableFeature<SendMessageFeature>());
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context testContext;

            public MyMessageHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.Received = true;

                return Task.CompletedTask;
            }
        }

        class SendMessageFeature : Feature
        {
            protected override void Setup(FeatureConfigurationContext context)
            {
                context.RegisterStartupTask(builder => new StartupTask());
            }
        }

        class StartupTask : FeatureStartupTask
        {
            protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                using (var tx = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await session.SendLocal(new MyMessage(), cancellationToken);

                    tx.Complete();
                }
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }

    class Context : ScenarioContext
    {
        public bool Received { get; set; }
    }

    class MyMessage : IMessage { }
}