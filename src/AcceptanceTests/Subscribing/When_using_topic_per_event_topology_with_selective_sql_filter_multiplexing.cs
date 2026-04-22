namespace NServiceBus.AcceptanceTests.NativePubSub;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Azure.Messaging.ServiceBus.Administration;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Transport.AzureServiceBus;
using Transport.AzureServiceBus.AcceptanceTests;

public class When_using_topic_per_event_topology_with_selective_sql_filter_multiplexing : NServiceBusAcceptanceTest
{
    static readonly string SharedTopicName = "SelectiveSqlFilterMultiplexing";

    [SetUp]
    public async Task Setup()
    {
        var adminClient = new ServiceBusAdministrationClient(
            Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

        await CleanupEntity(adminClient, SharedTopicName);
        await adminClient.CreateTopicAsync(SharedTopicName);
    }

    [TearDown]
    public async Task Teardown()
    {
        var adminClient = new ServiceBusAdministrationClient(
            Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

        await CleanupEntity(adminClient, SharedTopicName);
    }

    static async Task CleanupEntity(ServiceBusAdministrationClient adminClient, string topicName)
    {
        if (await adminClient.TopicExistsAsync(topicName))
        {
            await adminClient.DeleteTopicAsync(topicName);
        }
    }

    [Test]
    public async Task Should_deliver_only_matching_events_to_each_subscriber()
    {
        Requires.NativePubSubSupport();

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(async (session, c) =>
            {
                await session.Publish(new MyEvent1()).ConfigureAwait(false);
                await session.Publish(new MyEvent2()).ConfigureAwait(false);
            }))
            .WithEndpoint<SubscriberForEvent1>()
            .WithEndpoint<SubscriberForEvent2>()
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Subscriber1GotMyEvent1, Is.True);
            Assert.That(context.Subscriber1GotMyEvent2, Is.False);
            Assert.That(context.Subscriber2GotMyEvent1, Is.False);
            Assert.That(context.Subscriber2GotMyEvent2, Is.True);
        }
    }

    public class Context : ScenarioContext
    {
        public bool Subscriber1GotMyEvent1 { get; set; }
        public bool Subscriber1GotMyEvent2 { get; set; }
        public bool Subscriber2GotMyEvent1 { get; set; }
        public bool Subscriber2GotMyEvent2 { get; set; }

        public void MaybeMarkAsCompleted() =>
            MarkAsCompleted(Subscriber1GotMyEvent1, Subscriber2GotMyEvent2);
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = TopicTopology.Default;
                topology.PublishTo<MyEvent1>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);
                topology.PublishTo<MyEvent2>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);
                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata =>
            {
                metadata.RegisterSelfAsPublisherFor<MyEvent1>(this);
                metadata.RegisterSelfAsPublisherFor<MyEvent2>(this);
            });
    }

    public class SubscriberForEvent1 : EndpointConfigurationBuilder
    {
        public SubscriberForEvent1() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = TopicTopology.Default;
                var endpointName = Conventions.EndpointNamingConvention(typeof(SubscriberForEvent1));
                topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                topology.SubscribeTo<MyEvent1>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);
                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata =>
            {
                metadata.RegisterPublisherFor<MyEvent1>(typeof(Publisher));
                metadata.RegisterPublisherFor<MyEvent2>(typeof(Publisher));
            });

        public class Handler(Context context) : IHandleMessages<MyEvent1>, IHandleMessages<MyEvent2>
        {
            public Task Handle(MyEvent1 message, IMessageHandlerContext handlerContext)
            {
                context.Subscriber1GotMyEvent1 = true;
                context.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }

            public Task Handle(MyEvent2 message, IMessageHandlerContext handlerContext)
            {
                context.Subscriber1GotMyEvent2 = true;
                return Task.CompletedTask;
            }
        }
    }

    public class SubscriberForEvent2 : EndpointConfigurationBuilder
    {
        public SubscriberForEvent2() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = TopicTopology.Default;
                var endpointName = Conventions.EndpointNamingConvention(typeof(SubscriberForEvent2));
                topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                topology.SubscribeTo<MyEvent2>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);
                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata =>
            {
                metadata.RegisterPublisherFor<MyEvent1>(typeof(Publisher));
                metadata.RegisterPublisherFor<MyEvent2>(typeof(Publisher));
            });

        public class Handler(Context context) : IHandleMessages<MyEvent1>, IHandleMessages<MyEvent2>
        {
            public Task Handle(MyEvent1 message, IMessageHandlerContext handlerContext)
            {
                context.Subscriber2GotMyEvent1 = true;
                return Task.CompletedTask;
            }

            public Task Handle(MyEvent2 message, IMessageHandlerContext handlerContext)
            {
                context.Subscriber2GotMyEvent2 = true;
                context.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent1 : IEvent;
    public class MyEvent2 : IEvent;
}
