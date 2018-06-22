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
        ManagementClient client;

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


        [Test]
        public async Task Create_endpoint_when_there_are_no_entities()
        {
            await DeleteQueue("abc");
            await DeleteTopic("cli-topic");

            var (output, error, exitCode) = await Execute("endpoint create abc --topic cli-topic");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);
            Assert.IsFalse(output.Contains("skipping"));

            await VerifyQueue("abc");
            await VerifyTopic("cli-topic");
            await VerifySubscription(topicName:"cli-topic", subscriptionName:"abc", queueName:"abc");
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

        async Task VerifySubscription(string topicName, string subscriptionName, string queueName, bool enablePartitioning = false, int size = 5 * 1024)
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
    }
}