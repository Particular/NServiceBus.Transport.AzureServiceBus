namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    using NUnit.Framework;

    public class When_using_hierarchy_namespace : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_send_and_receive_with_hierarchy_namespace()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                    });

                    endpoint.When(async session => await session.Send(Conventions.EndpointNamingConvention(typeof(HierarchyReceiver)).Shorten(), new MyMessage()));
                })
                .WithEndpoint<ExternalReceiver>()
                .WithEndpoint<HierarchyReceiver>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                    });
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.HierarchyMessageReceived, Is.True);
                Assert.That(context.ExternalMessageReceived, Is.False);
            }
        }

        public class Context : ScenarioContext
        {
            public bool HierarchyMessageReceived { get; set; }
            public bool ExternalMessageReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServer>();
        }

        class HierarchyReceiver : EndpointConfigurationBuilder
        {
            public HierarchyReceiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }
        }

        class ExternalReceiver : EndpointConfigurationBuilder
        {
            public ExternalReceiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ExternalMessageReceived = true;
                    testContext.MarkAsFailed(new Exception("External receiver should not receive the hierarchy message"));
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;
    }
}