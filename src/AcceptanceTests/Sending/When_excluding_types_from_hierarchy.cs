namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    using NUnit.Framework;

    public class When_excluding_types_from_hierarchy : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_exclude_configured_message_types_from_hierarchy_namespace()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                        transport.HierarchyNamespaceOptions.ExcludeMessageType<MyMessage>();
                    });

                    endpoint.When(async session => await session.Send(Conventions.EndpointNamingConvention(typeof(ExternalReceiver)).Shorten(), new MyMessage()));
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
                Assert.That(context.HierarchyMessageReceived, Is.False);
                Assert.That(context.ExternalMessageReceived, Is.True);
            }
        }

        class Context : ScenarioContext
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
                    testContext.MarkAsFailed(new Exception("Hierarchy receiver should not receive the excluded message"));
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
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;
    }
}