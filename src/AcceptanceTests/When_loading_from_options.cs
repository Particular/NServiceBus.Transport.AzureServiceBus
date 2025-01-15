namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
    using System;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public partial class When_loading_from_options
    {
        string TopicName;

        [SetUp]
        public async Task Setup()
        {
            TopicName = $"Publisher{Base64Regex().Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "").ToUpperInvariant()}";
            await TestContext.Out.WriteLineAsync($"Generated name prefix: '{TopicName}'");

            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteTopicAsync(TopicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        [Test]
        public async Task Should_allow_topic_per_event_type_options() =>
            await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        // doing a deliberate roundtrip to ensure that the options are correctly serialized and deserialized
                        var serializedOptions = JsonSerializer.Serialize(new TopicPerEventTopologyOptions
                        {
                            SubscriptionName = TopicName,
                            PublishedEventToTopicsMap = { { typeof(Event).FullName, TopicName } },
                            SubscribedEventToTopicsMap = { { typeof(Event).FullName, [TopicName] } }
                        }, TopologyOptionsSerializationContext.Default.TopologyOptions);
                        var options = JsonSerializer.Deserialize(serializedOptions, TopologyOptionsSerializationContext.Default.TopologyOptions);
                        transport.Topology = TopicTopology.FromOptions(options);
                    });
                    b.When((session, c) => session.Publish(new Event()));
                })
                .Done(c => c.EventReceived)
                .Run();

        [Test]
        public async Task Should_allow_migration_options() =>
            await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        // doing a deliberate roundtrip to ensure that the options are correctly serialized and deserialized
                        var serializedOptions = JsonSerializer.Serialize(new MigrationTopologyOptions
                        {
                            TopicToPublishTo = TopicName,
                            TopicToSubscribeOn = TopicName,
                            EventsToMigrateMap = [typeof(Event).FullName]
                        }, TopologyOptionsSerializationContext.Default.TopologyOptions);
                        var options = JsonSerializer.Deserialize(serializedOptions, TopologyOptionsSerializationContext.Default.TopologyOptions);
                        transport.Topology = TopicTopology.FromOptions(options);
                    });
                    b.When((session, c) => session.Publish(new Event()));
                })
                .Done(c => c.EventReceived)
                .Run();

        public class Context : ScenarioContext
        {
            public bool EventReceived { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
                EndpointSetup<DefaultServer>(b => { }, metadata =>
                {
                    metadata.RegisterSelfAsPublisherFor<Event>(this);
                });

            public class Handler(Context testContext) : IHandleMessages<Event>
            {
                public Task Handle(Event request, IMessageHandlerContext context)
                {
                    testContext.EventReceived = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class Event : IEvent;

        [GeneratedRegex("[/+=]")]
        private static partial Regex Base64Regex();
    }
}