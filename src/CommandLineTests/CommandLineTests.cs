﻿namespace NServiceBus.Transport.AzureServiceBus.CommandLine.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
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
            await VerifySubscriptionContainsOnlyDefaultRule(TopicName, SubscriptionName);
        }

        [Test]
        public async Task Subscribe_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            await Execute($"endpoint create {EndpointName} --topic {TopicName}");

            await Execute($"endpoint subscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifySubscription(TopicName, SubscriptionName, QueueName);
        }

        [Test]
        public async Task Unsubscribe_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            await Execute($"endpoint create {EndpointName} --topic {TopicName}");
            await Execute($"endpoint subscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");

            await Execute($"endpoint unsubscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");

            await VerifySubscriptionContainsOnlyDefaultRule(TopicName, SubscriptionName);
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
        public async Task Create_queue_when_it_exists_should_skip()
        {
            await DeleteQueue(QueueName);
            await Execute($"queue create {QueueName}");

            var (output, error, exitCode) = await Execute($"queue create {QueueName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);
            Assert.IsTrue(output.Contains("skipping"));

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
            client = new ServiceBusAdministrationClient(Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));
        }

        async Task VerifyQueue(string queueName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = (await client.GetQueueAsync(queueName)).Value;

            Assert.AreEqual(int.MaxValue, actual.MaxDeliveryCount);
            Assert.AreEqual(TimeSpan.FromMinutes(5), actual.LockDuration);
            Assert.AreEqual(true, actual.EnableBatchedOperations);
            Assert.AreEqual(enablePartitioning, actual.EnablePartitioning);
            Assert.AreEqual(size, actual.MaxSizeInMegabytes);
        }

        async Task VerifyTopic(string topicName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = (await client.GetTopicAsync(topicName)).Value;

            Assert.AreEqual(true, actual.EnableBatchedOperations);
            Assert.AreEqual(enablePartitioning, actual.EnablePartitioning);
            Assert.AreEqual(size, actual.MaxSizeInMegabytes);
        }

        async Task VerifySubscription(string topicName, string subscriptionName, string queueName)
        {
            var actual = (await client.GetSubscriptionAsync(topicName, subscriptionName)).Value;

            Assert.AreEqual(TimeSpan.FromMinutes(5), actual.LockDuration);
            Assert.IsTrue(actual.ForwardTo.EndsWith($"/{queueName}", StringComparison.Ordinal));
            Assert.AreEqual(false, actual.EnableDeadLetteringOnFilterEvaluationExceptions);
            Assert.AreEqual(int.MaxValue, actual.MaxDeliveryCount);
            Assert.AreEqual(true, actual.EnableBatchedOperations);
            Assert.AreEqual(queueName, actual.UserMetadata);

            // rules
            var rules = await client.GetRulesAsync(topicName, subscriptionName).ToListAsync();
            Assert.IsTrue(rules.Count == 4);

            var defaultRule = rules.ElementAt(0);
            Assert.AreEqual("$default", defaultRule.Name);
            Assert.AreEqual(new FalseRuleFilter().SqlExpression, ((FalseRuleFilter)defaultRule.Filter).SqlExpression);

            var customRuleNameRule = rules.ElementAt(1);
            Assert.AreEqual("CustomRuleName", customRuleNameRule.Name);
            Assert.AreEqual(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyNamespace1.MyMessage3%'").SqlExpression, ((SqlRuleFilter)customRuleNameRule.Filter).SqlExpression);

            var myMessage1Rule = rules.ElementAt(2);
            Assert.AreEqual("MyMessage1", myMessage1Rule.Name);
            Assert.AreEqual(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyMessage1%'").SqlExpression, ((SqlRuleFilter)myMessage1Rule.Filter).SqlExpression);

            var myMessage2WithNamespace = rules.ElementAt(3);
            Assert.AreEqual("MyNamespace1.MyMessage2", myMessage2WithNamespace.Name);
            Assert.AreEqual(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyNamespace1.MyMessage2%'").SqlExpression, ((SqlRuleFilter)myMessage2WithNamespace.Filter).SqlExpression);
        }

        async Task VerifySubscriptionContainsOnlyDefaultRule(string topicName, string subscriptionName)
        {
            // rules
            var rules = await client.GetRulesAsync(topicName, subscriptionName).ToListAsync();
            Assert.IsTrue(rules.Count == 1);

            var defaultRule = rules.ElementAt(0);
            Assert.AreEqual("$default", defaultRule.Name);
            Assert.AreEqual(new FalseRuleFilter().SqlExpression, ((FalseRuleFilter)defaultRule.Filter).SqlExpression);
        }

        async Task VerifyQueueExists(bool queueShouldExist)
        {
            var queueExists = (await client.QueueExistsAsync(QueueName)).Value;
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
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        async Task DeleteTopic(string topicName)
        {
            try
            {
                await client.DeleteTopicAsync(topicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        ServiceBusAdministrationClient client;
    }
}