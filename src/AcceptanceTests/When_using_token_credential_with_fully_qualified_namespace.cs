namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Identity;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_token_credential_with_fully_qualified_namespace : NServiceBusAcceptanceTest
    {
        string fullyQualifiedNamespace;

        [SetUp]
        public void Setup()
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");
            var connectionStringProperties = ServiceBusConnectionStringProperties.Parse(connectionString);
            fullyQualifiedNamespace = connectionStringProperties.FullyQualifiedNamespace;
        }

        [Test]
        public async Task Should_work()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        transport.FullyQualifiedNamespace = fullyQualifiedNamespace;
                        transport.TokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            Diagnostics =
                            {
                                IsLoggingEnabled = true,
                            }
                        });
                    });
                    b.When(session => session.SendLocal(new MyCommand()));
                })
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        transport.FullyQualifiedNamespace = fullyQualifiedNamespace;
                        transport.TokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            Diagnostics =
                            {
                                IsLoggingEnabled = true,
                            }
                        });
                    });
                })
                .Done(c => c.SubscriberGotEvent)
                .Run();

            Assert.True(context.SubscriberGotEvent);
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() => EndpointSetup<DefaultPublisher>();

            public class MyHandler : IHandleMessages<MyCommand>
            {
                public Task Handle(MyCommand message, IMessageHandlerContext context)
                    => context.Publish(new MyEvent());
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() => EndpointSetup<DefaultServer>();

            public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.SubscriberGotEvent = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent;

        public class MyCommand : ICommand;
    }
}