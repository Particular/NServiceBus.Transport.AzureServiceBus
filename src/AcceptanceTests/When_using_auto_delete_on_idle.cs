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
        const string HasAutoDeleteOnIdleEndpointInstanceName = "usingautodeleteonidle.endpointwithautodeleteonidle-12345";
        const string NoAutoDeleteOnIdleEndpointInstanceName = "usingautodeleteonidle.endpointwithoutautodeleteonidle";
        const string HasAutoDeleteOnIdleButNoInstancesEndpointName = "usingautodeleteonidle.endpointwithautodeleteonidlebutnoinstances";

        [SetUp]
        public async Task Setup()
        {
            var adminClient =
                new ServiceBusAdministrationClient(
                    Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
            try
            {
                // makes sure during local development the topic gets cleared before each test run
                await adminClient.DeleteQueueAsync(HasAutoDeleteOnIdleEndpointInstanceName);
                await adminClient.DeleteQueueAsync(NoAutoDeleteOnIdleEndpointInstanceName);
                await adminClient.DeleteQueueAsync(HasAutoDeleteOnIdleButNoInstancesEndpointName);
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

            var queueProperties = await adminClient.GetQueueAsync(HasAutoDeleteOnIdleEndpointInstanceName);

            Assert.That(queueProperties.Value.AutoDeleteOnIdle, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [Test]
        public async Task Should_not_configure_queue_with_auto_delete_on_idle()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithoutAutoDeleteOnIdle>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.ConfigureTransport<AzureServiceBusTransport>();
                    });
                })
                .Done(c => c.EndpointsStarted)
                .Run();

            // Verify that the queue was created with the correct AutoDeleteOnIdle setting
            var adminClient = new ServiceBusAdministrationClient(
                Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

            var queueProperties = await adminClient.GetQueueAsync(NoAutoDeleteOnIdleEndpointInstanceName);

            Assert.That(queueProperties.Value.AutoDeleteOnIdle, Is.EqualTo(TimeSpan.MaxValue));
        }

        [Test]
        public async Task Should_not_configure_queue_with_auto_delete_on_idle_if_no_unique_instances()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithAutoDeleteOnIdleButNoInstances>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = c.ConfigureTransport<AzureServiceBusTransport>();
                        transport.AutoDeleteOnIdle = TimeSpan.FromMinutes(10);
                    });
                })
                .Done(c => c.EndpointsStarted)
                .Run();

            // Verify that the queue was created with the correct AutoDeleteOnIdle setting
            var adminClient = new ServiceBusAdministrationClient(
                Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

            var queueProperties = await adminClient.GetQueueAsync(HasAutoDeleteOnIdleButNoInstancesEndpointName);

            Assert.That(queueProperties.Value.AutoDeleteOnIdle, Is.EqualTo(TimeSpan.MaxValue));
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

        public class EndpointWithoutAutoDeleteOnIdle : EndpointConfigurationBuilder
        {
            public EndpointWithoutAutoDeleteOnIdle()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class EndpointWithAutoDeleteOnIdleButNoInstances : EndpointConfigurationBuilder
        {
            public EndpointWithAutoDeleteOnIdleButNoInstances()
            {
                EndpointSetup<DefaultServer>();
            }
        }
    }
}