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

        [Test]
        public async Task Should_publish_and_receive_with_hierarchy_namespace()
        {
            Requires.NativePubSubSupport();
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                    });
                    endpoint.When(async session => await session.Publish(new MyEvent()));
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
            public Sender() => EndpointSetup<DefaultServer>(
                config => { },
                publishMetadata => publishMetadata.RegisterSelfAsPublisherFor<MyEvent>(this)
            );
        }

        class HierarchyReceiver : EndpointConfigurationBuilder
        {
            public HierarchyReceiver() => EndpointSetup<DefaultServer>(
                config => { },
                publishMetadata => publishMetadata.RegisterPublisherFor<MyEvent, Sender>());

            public class MyHandler(Context testContext) :
                IHandleMessages<MyMessage>,
                IHandleMessages<MyEvent>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsCompleted();
                    return Task.CompletedTask;
                }
            }

        }

        class ExternalReceiver : EndpointConfigurationBuilder
        {
            public ExternalReceiver() => EndpointSetup<DefaultServer>(
                config => { },
                publishMetadata => publishMetadata.RegisterPublisherFor<MyEvent, Sender>()
            );

            public class MyMessageHandler(Context testContext) :
                IHandleMessages<MyMessage>,
                IHandleMessages<MyEvent>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ExternalMessageReceived = true;
                    testContext.MarkAsFailed(new Exception("External receiver should not receive the hierarchy message"));
                    return Task.CompletedTask;
                }
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.ExternalMessageReceived = true;
                    testContext.MarkAsFailed(new Exception("External receiver should not receive the hierarchy event"));
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;
        public class MyEvent : IEvent;
    }
}