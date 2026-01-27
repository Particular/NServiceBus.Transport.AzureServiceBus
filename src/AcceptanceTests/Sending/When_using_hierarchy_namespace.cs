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

                    endpoint.When(async session => await session.Send(Conventions.EndpointNamingConvention(typeof(Receiver)).Shorten(), new MyMessage()));
                })
                .WithEndpoint<Receiver>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                    });
                })
                .Done(c => c.MessageReceived)
                .Run();

            Assert.That(context.MessageReceived, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServer>();
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;
    }
}
