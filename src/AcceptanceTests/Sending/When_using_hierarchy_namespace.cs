// new acceptance test to verify HierarchyNamespace configuration
namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
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
                .WithEndpoint<HierarchyReceiver>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                    });
                })
                .Done(c => c.HierarchyMessageReceived)
                .Run();

            Assert.That(context.HierarchyMessageReceived, Is.True);
        }

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
                .Done(c => c.ExternalMessageReceived)
                .Run();

            Assert.That(context.HierarchyMessageReceived, Is.False);
            Assert.That(context.ExternalMessageReceived, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool HierarchyMessageReceived { get; set; }
            public bool ExternalMessageReceived { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServer>();
        }

        public class HierarchyReceiver : EndpointConfigurationBuilder
        {
            public HierarchyReceiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class ExternalReceiver : EndpointConfigurationBuilder
        {
            public ExternalReceiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ExternalMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;

    }
}
