namespace NServiceBus.AcceptanceTests.NativePubSub;

using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Azure.Messaging.ServiceBus.Administration;
using Logging;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Configuration.AdvancedExtensibility;
using NUnit.Framework;
using Transport.AzureServiceBus;
using Transport.AzureServiceBus.AcceptanceTests;

public class When_subscribing_with_least_privilege_and_correlation_filter : NServiceBusAcceptanceTest
{
    static readonly string SharedTopicName = "LeastPrivilegeCorrelationFilter";

    [SetUp]
    public async Task Setup()
    {
        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());

        await CleanupEntity(adminClient, AcceptanceTestEntityNames.For(SharedTopicName));
    }

    [TearDown]
    public async Task Teardown()
    {
        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());

        await CleanupEntity(adminClient, AcceptanceTestEntityNames.For(SharedTopicName));
    }

    static async Task CleanupEntity(ServiceBusAdministrationClient adminClient, string topicName)
    {
        if (await adminClient.TopicExistsAsync(topicName))
        {
            await adminClient.DeleteTopicAsync(topicName);
        }
    }

    [Test]
    public async Task Should_subscribe_without_manage_rights_when_subscription_exists()
    {
        Requires.NativePubSubSupport();

        // First run with manage rights so the topic and the subscription exist
        await Scenario.Define<Context>()
            .WithEndpoint<Publisher>()
            .WithEndpoint<Subscriber>()
            .Done(c => c.EndpointsStarted)
            .Run();

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b =>
            {
                b.CustomConfig(UseLeastPrivilege);
                b.When(session => session.Publish(new MyEvent()));
            })
            .WithEndpoint<Subscriber>(b => b.CustomConfig(UseLeastPrivilege))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.SubscriberGotEvent, Is.True);
            Assert.That(context.Logs.Where(l => l.Level == LogLevel.Error).Select(l => l.Message), Is.Empty);
        }
    }

    [Test]
    public async Task Should_report_failure_without_manage_rights_when_subscription_is_missing()
    {
        Requires.NativePubSubSupport();

        var adminClient = new ServiceBusAdministrationClient(AcceptanceTestConnectionString.Get());
        await adminClient.CreateTopicAsync(AcceptanceTestEntityNames.For(SharedTopicName));

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Subscriber>(b => b.CustomConfig(UseLeastPrivilege))
            .Done(c => c.EndpointsStarted)
            .Run();

        Assert.That(context.Logs.Where(l => l.Level == LogLevel.Error).Select(l => l.Message),
            Has.Some.Contains("could not be created"));
    }

    static void UseLeastPrivilege(EndpointConfiguration c)
    {
        c.GetSettings().Set("Installers.Enable", false);
        c.ConfigureTransport<AzureServiceBusTransport>().ConnectionString = AcceptanceTestConnectionString.RestrictedConnectionString;
    }

    public class Context : ScenarioContext
    {
        public bool SubscriberGotEvent { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = TopicTopology.Default;
                topology.PublishTo<MyEvent>(SharedTopicName, opts => opts.Mode = TopicRoutingMode.CorrelationFilter);
                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata => metadata.RegisterSelfAsPublisherFor<MyEvent>(this));
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = TopicTopology.Default;
                var endpointName = Conventions.EndpointNamingConvention(typeof(Subscriber));
                topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                topology.SubscribeTo<MyEvent>(SharedTopicName, opts => opts.Mode = TopicRoutingMode.CorrelationFilter);
                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata => metadata.RegisterPublisherFor<MyEvent, Publisher>());

        [Handler]
        public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                testContext.SubscriberGotEvent = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}
