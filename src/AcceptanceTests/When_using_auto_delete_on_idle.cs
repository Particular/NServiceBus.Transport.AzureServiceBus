namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_auto_delete_on_idle : NServiceBusAcceptanceTest
    {
        const string ResultingEndpointInstanceName = "usingautodeleteonidle.endpointwithautodeleteonidle-12345";
        [SetUp]
        public async Task Setup()
        {
            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteQueueAsync(ResultingEndpointInstanceName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        [Test]
        public async Task Should_configure_queue_with_auto_delete_on_idle()
        {
            var instanceId = "12345";
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithAutoDeleteOnIdle>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        transport.AutoDeleteOnIdle = TimeSpan.FromMinutes(10);
                        c.MakeInstanceUniquelyAddressable(instanceId);
                    });
                })
                .Done(c => c.EndpointsStarted)
                .Run();

            // Verify that the queue was created with the correct AutoDeleteOnIdle setting
            var adminClient = new ServiceBusAdministrationClient(
                Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

            var queueProperties = await adminClient.GetQueueAsync(ResultingEndpointInstanceName);

            Assert.That(queueProperties.Value.AutoDeleteOnIdle, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        public class Context : ScenarioContext
        {
        }

        public class EndpointWithAutoDeleteOnIdle : EndpointConfigurationBuilder
        {
            public EndpointWithAutoDeleteOnIdle()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}
