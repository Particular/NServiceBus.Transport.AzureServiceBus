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

public class When_using_topic_per_event_topology_with_mapped_and_fallback_sql_filter_on_shared_topic : NServiceBusAcceptanceTest
{
    static readonly string SharedTopicName = "MappedAndFallbackSqlFilterSharedTopic";

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
    public async Task Should_deliver_mapped_and_unmapped_events_when_both_target_same_shared_topic()
    {
        Requires.NativePubSubSupport();

        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(async (session, c) =>
            {
                await session.Publish(new MyEvent1 { Data = "event1" }).ConfigureAwait(false);
                await session.Publish(new MyEvent2 { Data = "event2" }).ConfigureAwait(false);
            }))
            .WithEndpoint<Subscriber>()
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.SubscriberGotMyEvent1, Is.True);
            Assert.That(context.SubscriberGotMyEvent2, Is.True);
        }
    }

    public class Context : ScenarioContext
    {
        public bool SubscriberGotMyEvent1 { get; set; }
        public bool SubscriberGotMyEvent2 { get; set; }

        public void MaybeMarkAsCompleted() =>
            MarkAsCompleted(SubscriberGotMyEvent1, SubscriberGotMyEvent2);
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var topology = (TopicPerEventTopology)TopicTopology.FromOptions(new TopologyOptions
                {
                    FallbackTopic = new FallbackTopicOptions
                    {
                        TopicName = SharedTopicName,
                        Mode = TopicRoutingMode.SqlFilter
                    }
                });
                topology.PublishTo<MyEvent1>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);

                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata =>
            {
                metadata.RegisterSelfAsPublisherFor<MyEvent1>(this);
                metadata.RegisterSelfAsPublisherFor<MyEvent2>(this);
            });
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var endpointName = Conventions.EndpointNamingConvention(typeof(Subscriber));
                var topology = (TopicPerEventTopology)TopicTopology.FromOptions(new TopologyOptions
                {
                    FallbackTopic = new FallbackTopicOptions
                    {
                        TopicName = SharedTopicName,
                        Mode = TopicRoutingMode.SqlFilter
                    }
                });
                topology.OverrideSubscriptionNameFor(endpointName, endpointName.Shorten());
                topology.SubscribeTo<IMyEvent>(SharedTopicName, options => options.Mode = TopicRoutingMode.SqlFilter);

                c.ConfigureTransport<AzureServiceBusTransport>().Topology = topology;
            }, metadata =>
            {
                metadata.RegisterPublisherFor<MyEvent1>(typeof(Publisher));
                metadata.RegisterPublisherFor<MyEvent2>(typeof(Publisher));
                metadata.RegisterPublisherFor<IMyEvent>("not-used");
            });

        public class MyHandler(Context testContext) : IHandleMessages<IMyEvent>
        {
            public Task Handle(IMyEvent messageThatIsEnlisted, IMessageHandlerContext context)
            {
                switch (messageThatIsEnlisted)
                {
                    case MyEvent1:
                        testContext.SubscriberGotMyEvent1 = true;
                        break;
                    case MyEvent2:
                        testContext.SubscriberGotMyEvent2 = true;
                        break;
                    default:
                        break;
                }

                testContext.MaybeMarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent1 : IMyEvent
    {
        public string Data { get; set; } = string.Empty;
    }

    public class MyEvent2 : IMyEvent
    {
        public string Data { get; set; } = string.Empty;
    }

    public interface IMyEvent : IEvent;
}
