namespace NServiceBus.Transport.AzureServiceBus.CommandLine.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using NUnit.Framework;

    [TestFixture]
    public class CommandLineTests
    {
        const string EndpointName = "cli-queue";
        const string QueueName = EndpointName;
        const string TopicName = "cli-topic";
        const string SubscriptionName = QueueName;

        [Test]
        public async Task Create_endpoint_when_there_are_no_entities()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName} --topic {TopicName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);
            Assert.IsFalse(output.Contains("skipping"));

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifySubscription(TopicName, SubscriptionName, QueueName);
        }

        [Test]
        public async Task Create_queue_when_it_does_not_exist()
        {
            await DeleteQueue(QueueName);

            var (output, error, exitCode) = await Execute($"queue create {QueueName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);
            Assert.IsFalse(output.Contains("skipping"));

            await VerifyQueue(QueueName);
        }

        [Test]
        public async Task Create_queue_when_it_exists_should_throw()
        {
            await DeleteQueue(QueueName);
            await Execute($"queue create {QueueName}");

            var (_, error, exitCode) = await Execute($"queue create {QueueName}");

            Assert.AreEqual(1, exitCode);
            Assert.IsTrue(error.Contains(nameof(MessagingEntityAlreadyExistsException)));

            await VerifyQueue(QueueName);
        }

        [Test]
        public async Task Delete_queue_when_it_exists()
        {
            await DeleteQueue(QueueName);
            await Execute($"queue create {QueueName}");

            var (_, error, exitCode) = await Execute($"queue delete {QueueName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueueExists(false);
        }

        [SetUp]
        public void Setup()
        {
            client = new ManagementClient(Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
        }

        [TearDown]
        public Task TearDown()
        {
            return client.CloseAsync();
        }

        async Task VerifyQueue(string queueName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = await client.GetQueueAsync(queueName);

            Assert.AreEqual(int.MaxValue, actual.MaxDeliveryCount);
            Assert.AreEqual(TimeSpan.FromMinutes(5), actual.LockDuration);
            Assert.AreEqual(true, actual.EnableBatchedOperations);
            Assert.AreEqual(enablePartitioning, actual.EnablePartitioning);
            Assert.AreEqual(size, actual.MaxSizeInMB);
        }

        async Task VerifyTopic(string topicName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = await client.GetTopicAsync(topicName);

            Assert.AreEqual(true, actual.EnableBatchedOperations);
            Assert.AreEqual(enablePartitioning, actual.EnablePartitioning);
            Assert.AreEqual(size, actual.MaxSizeInMB);
        }

        async Task VerifySubscription(string topicName, string subscriptionName, string queueName)
        {
            var actual = await client.GetSubscriptionAsync(topicName, subscriptionName);

            Assert.AreEqual(TimeSpan.FromMinutes(5), actual.LockDuration);
            Assert.IsTrue(actual.ForwardTo.EndsWith($"/{queueName}"));
            Assert.AreEqual(false, actual.EnableDeadLetteringOnFilterEvaluationExceptions);
            Assert.AreEqual(int.MaxValue, actual.MaxDeliveryCount);
            // TODO: uncomment when https://github.com/Azure/azure-service-bus-dotnet/issues/499 is fixed
            //Assert.AreEqual(true, actual.EnableBatchedOperations);
            // TODO: https://github.com/Azure/azure-service-bus-dotnet/issues/501 is fixed
            //Assert.AreEqual(queueName, actual.UserMetadata);

            // rules
            var rules = await client.GetRulesAsync(topicName, subscriptionName);
            Assert.IsEmpty(rules);
        }

        async Task VerifyQueueExists(bool queueShouldExist)
        {
            var queueExists = await client.QueueExistsAsync(QueueName);
            Assert.AreEqual(queueShouldExist, queueExists);
        }

        static async Task<(string output, string error, int exitCode)> Execute(string command)
        {
            var process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = TestContext.CurrentContext.TestDirectory;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = "NServiceBus.Transport.AzureServiceBus.CommandLine.dll " + command;

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit(10000);

            var output = await outputTask;
            var error = await errorTask;

            if (output != string.Empty)
            {
                Console.WriteLine(output);
            }

            if (error != string.Empty)
            {
                Console.WriteLine(error);
            }

            return (output, error, process.ExitCode);
        }

        async Task DeleteQueue(string queueName)
        {
            try
            {
                await client.DeleteQueueAsync(queueName);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
        }

        async Task DeleteTopic(string topicName)
        {
            try
            {
                await client.DeleteTopicAsync(topicName);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
        }

        async Task DeleteSubscription(string topicName, string subscriptionName)
        {
            try
            {
                await client.DeleteSubscriptionAsync(topicName, subscriptionName);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
        }

        ManagementClient client;
    }
}