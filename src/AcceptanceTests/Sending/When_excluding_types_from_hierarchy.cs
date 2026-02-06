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
            Requires.NativePubSubSupport();
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(endpoint =>
                {
                    endpoint.CustomConfig(cfg =>
                    {
                        var transport = cfg.ConfigureTransport<AzureServiceBusTransport>();
                        transport.HierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "my-hierarchy" };
                        transport.HierarchyNamespaceOptions.ExcludeMessageType<MyMessage>();
                        transport.HierarchyNamespaceOptions.ExcludeMessageType<MyEvent>();
                    });

                    endpoint.When(async session =>
                    {
                        await session.Send(Conventions.EndpointNamingConvention(typeof(ExternalReceiver)).Shorten(), new MyMessage());
                        await session.Publish(new MyEvent());
                    });
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
                .Done(c => c.ExternalMessageReceived && c.ExternalEventReceived)
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
            public bool ExternalEventReceived { get; set; }
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
                publishMetadata => publishMetadata.RegisterPublisherFor<MyEvent,Sender>());

            public class MyMessageHandler(Context testContext) :
                IHandleMessages<MyMessage>,
                IHandleMessages<MyEvent>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsFailed(new Exception("Hierarchy receiver should not receive the excluded message"));
                    return Task.CompletedTask;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.HierarchyMessageReceived = true;
                    testContext.MarkAsFailed(new Exception("Hierarchy receiver should not receive the published event"));
                    return Task.CompletedTask;
                }
            }
        }

        class ExternalReceiver : EndpointConfigurationBuilder
        {
            public ExternalReceiver() => EndpointSetup<DefaultServer>(
                config => { },
                publishMetadata => publishMetadata.RegisterPublisherFor<MyEvent,Sender>()
            );

            public class MyMessageHandler(Context testContext) :
                IHandleMessages<MyMessage>,
                IHandleMessages<MyEvent>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ExternalMessageReceived = true;
                    return Task.CompletedTask;
                }
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.ExternalEventReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;
        public class MyEvent : IEvent;
    }
}