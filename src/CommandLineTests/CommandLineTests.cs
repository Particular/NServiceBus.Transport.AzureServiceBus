﻿namespace NServiceBus.Transport.AzureServiceBus.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
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
        const string DefaultTopicName = "bundle-1";
        const string HierarchyTopicName = "cli-topic-sub";
        const string SubscriptionName = QueueName;
        const string HierarchySubscriptionName = $"forwardTo-{HierarchyTopicName}";

        [Test]
        public async Task Create_endpoint()
        {
            await DeleteQueue(QueueName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output, Does.Not.Contain("skipping"));
            });

            await VerifyQueue(QueueName);
        }

        [Test]
        public async Task Create_endpoint_rejects_options_relevant_to_migration()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --topic-to-publish-to {TopicName}");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("Unrecognized option '--topic-to-publish-to'"));
        }

        [Test]
        public async Task Create_migration_endpoint_without_specifying_a_topic()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(DefaultTopicName);

            var (output, error, exitCode) = await Execute($"migration endpoint create {EndpointName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output, Does.Not.Contain("skipping"));
            });

            await VerifyQueue(QueueName);
            await VerifyTopic(DefaultTopicName);
            await VerifySubscriptionContainsOnlyDefaultRejectAllRule(DefaultTopicName, SubscriptionName);
        }

        [Test]
        public async Task Create_migration_endpoint_when_there_are_no_entities()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            var (output, error, exitCode) = await Execute($"migration endpoint create {EndpointName} --topic {TopicName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output, Does.Not.Contain("skipping"));
            });

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifySubscriptionContainsOnlyDefaultRejectAllRule(TopicName, SubscriptionName);
        }

        [Test]
        public async Task Create_migration_endpoint_with_hierarchy_when_there_are_no_entities()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);
            await DeleteTopic(HierarchyTopicName);

            var (output, error, exitCode) = await Execute($"migration endpoint create {EndpointName} --topic-to-publish-to {TopicName} --topic-to-subscribe-on {HierarchyTopicName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output, Does.Not.Contain("skipping"));
            });

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifyTopic(HierarchyTopicName);
            await VerifySubscriptionContainsOnlyDefaultMatchAllRule(TopicName, HierarchySubscriptionName);
            await VerifySubscriptionContainsOnlyDefaultRejectAllRule(HierarchyTopicName, SubscriptionName);
        }

        [Test]
        public async Task Create_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Create_migration_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"migration endpoint create {EndpointName} --topic {TopicName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Create_migration_endpoint_validates_topic_name_and_hierarchy_cannot_be_used_together()
        {
            var (_, publishError, publishExitCode) = await Execute($"migration endpoint create {EndpointName} --topic {TopicName} --topic-to-publish-to {HierarchyTopicName}");
            var (_, subscribeError, subscribeExitCode) = await Execute($"migration endpoint create {EndpointName} --topic {TopicName} --topic-to-subscribe-on {HierarchyTopicName}");

            Assert.Multiple(() =>
            {
                Assert.That(publishExitCode, Is.EqualTo(1));
                Assert.That(subscribeExitCode, Is.EqualTo(1));
            });
            Assert.That(publishError, Does.Contain("The --topic option and the --topic-to-publish-to option cannot be combined. Choose either a single topic name by specifying the --topic option or a hierarchy by specifying both the --topic-to-publish-to option, and --topic-to-subscribe-on option."));
            Assert.That(subscribeError, Does.Contain("The --topic option and the--topic-to-subscribe-on option cannot be combined. Choose either a single topic name by specifying the --topic option or a hierarchy by specifying both the --topic-to-publish-to option, and --topic-to-subscribe-on option."));
        }

        [Test]
        public async Task Create_migration_endpoint_validates_hierarchy_is_correct()
        {
            var (_, error, exitCode) = await Execute($"migration endpoint create {EndpointName} --topic-to-subscribe-on {HierarchyTopicName} --topic-to-publish-to {HierarchyTopicName}");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("A valid topic hierarchy requires the topic-to-publish-to option and the topic-to-subscribe-on option to be different."));
        }

        [Test]
        public async Task Subscribe_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic("MyMessage1");

            await Execute($"endpoint create {EndpointName}");
            await Execute($"endpoint subscribe {EndpointName} MyMessage1");

            await VerifyQueue(QueueName);
            await VerifyTopic("MyMessage1");
            await VerifyTopicPerEventTypeSubscription("MyMessage1", SubscriptionName, QueueName);
            await VerifySubscriptionContainsOnlyDefaultMatchAllRule("MyMessage1", SubscriptionName);
        }

        [Test]
        public async Task Subscribe_migration_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            await Execute($"migration endpoint create {EndpointName} --topic {TopicName}");

            await Execute($"migration endpoint subscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");
            await Execute($"migration endpoint subscribe-migrated {EndpointName} MyNamespace1.MyMessage4");

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifySingleTopicSubscription(TopicName, SubscriptionName, QueueName);
            await VerifyTopicPerEventTypeSubscription("MyNamespace1.MyMessage4", SubscriptionName, QueueName);
        }

        [Test]
        public async Task Subscribe_migration_endpoint_supports_hierarchy()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);
            await DeleteTopic(HierarchyTopicName);

            await Execute($"migration endpoint create {EndpointName} --topic-to-publish-to {TopicName} --topic-to-subscribe-on {HierarchyTopicName}");

            await Execute($"migration endpoint subscribe {EndpointName} MyMessage1 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {HierarchyTopicName} --rule-name CustomRuleName");

            await VerifyQueue(QueueName);
            await VerifyTopic(TopicName);
            await VerifyTopic(HierarchyTopicName);
            await VerifySingleTopicSubscription(HierarchyTopicName, SubscriptionName, QueueName);
        }

        [Test]
        public async Task Subscribe_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} MyMessage1 --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Subscribe_migration_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"migration endpoint subscribe {EndpointName} MyMessage1 --topic {TopicName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Unsubscribe_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic("MyMessage1");

            await Execute($"endpoint create {EndpointName}");
            await Execute($"endpoint subscribe {EndpointName} MyMessage1");
            await Execute($"endpoint unsubscribe {EndpointName} MyMessage1");

            await VerifyQueue(QueueName);
            await VerifyTopic("MyMessage1");
            await VerifySubscriptionDoesNotExist("MyMessage1", SubscriptionName);
        }

        [Test]
        public async Task Unsubscribe_migration_endpoint()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);

            await Execute($"migration endpoint create {EndpointName} --topic {TopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");
            await Execute($"migration endpoint subscribe-migrated {EndpointName} MyNamespace1.MyMessage4");

            await Execute($"migration endpoint unsubscribe {EndpointName} MyMessage1 --topic {TopicName}");
            await Execute($"migration endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage2 --topic {TopicName}");
            await Execute($"migration endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage3 --topic {TopicName} --rule-name CustomRuleName");
            await Execute($"migration endpoint unsubscribe-migrated {EndpointName} MyNamespace1.MyMessage4");

            await VerifySubscriptionContainsOnlyDefaultRejectAllRule(TopicName, SubscriptionName);
            await VerifySubscriptionDoesNotExist("MyNamespace1.MyMessage4", SubscriptionName);
        }

        [Test]
        public async Task Unsubscribe_migration_endpoint_supports_hierarchy()
        {
            await DeleteQueue(QueueName);
            await DeleteTopic(TopicName);
            await DeleteTopic(HierarchySubscriptionName);

            await Execute($"migration endpoint create {EndpointName} --topic-to-publish-to {TopicName} --topic-to-subscribe-on {HierarchyTopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyMessage1 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage2 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint subscribe {EndpointName} MyNamespace1.MyMessage3 --topic {HierarchyTopicName} --rule-name CustomRuleName");

            await Execute($"migration endpoint unsubscribe {EndpointName} MyMessage1 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage2 --topic {HierarchyTopicName}");
            await Execute($"migration endpoint unsubscribe {EndpointName} MyNamespace1.MyMessage3 --topic {HierarchyTopicName} --rule-name CustomRuleName");

            await VerifySubscriptionContainsOnlyDefaultRejectAllRule(HierarchyTopicName, SubscriptionName);
        }

        [Test]
        public async Task Unsubscribe_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"endpoint unsubscribe {EndpointName} MyMessage1 --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Unsubscribe_migration_endpoint_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"migration endpoint unsubscribe {EndpointName} MyMessage1 --topic {TopicName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Create_queue_when_it_does_not_exist()
        {
            await DeleteQueue(QueueName);

            var (output, error, exitCode) = await Execute($"queue create {QueueName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output, Does.Not.Contain("skipping"));
            });

            await VerifyQueue(QueueName);
        }

        [Test]
        public async Task Create_queue_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"queue create {QueueName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [Test]
        public async Task Create_queue_when_it_exists_should_skip()
        {
            await DeleteQueue(QueueName);
            await Execute($"queue create {QueueName}");

            var (output, error, exitCode) = await Execute($"queue create {QueueName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
                Assert.That(output.Contains("skipping"), Is.True);
            });

            await VerifyQueue(QueueName);
        }

        [Test]
        public async Task Delete_queue_when_it_exists()
        {
            await DeleteQueue(QueueName);
            await Execute($"queue create {QueueName}");

            var (_, error, exitCode) = await Execute($"queue delete {QueueName}");

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(error, Is.EqualTo(string.Empty));
            });

            await VerifyQueueExists(false);
        }

        [Test]
        public async Task Delete_queue_validates_namespace_and_connection_string_cannot_be_used_together()
        {
            var (_, error, exitCode) = await Execute($"queue delete {QueueName} --namespace somenamespace.servicebus.windows.net --connection-string someConnectionString");

            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("The connection string and the namespace option cannot be used together."));
        }

        [SetUp]
        public void Setup()
            => client = new ServiceBusAdministrationClient(Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

        async Task VerifyQueue(string queueName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = (await client.GetQueueAsync(queueName)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(actual.MaxDeliveryCount, Is.EqualTo(int.MaxValue));
                Assert.That(actual.LockDuration, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(actual.EnableBatchedOperations, Is.EqualTo(true));
                Assert.That(actual.EnablePartitioning, Is.EqualTo(enablePartitioning));
                Assert.That(actual.MaxSizeInMegabytes, Is.EqualTo(size));
            });
        }

        async Task VerifyTopic(string topicName, bool enablePartitioning = false, int size = 5 * 1024)
        {
            var actual = (await client.GetTopicAsync(topicName)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(actual.EnableBatchedOperations, Is.EqualTo(true));
                Assert.That(actual.EnablePartitioning, Is.EqualTo(enablePartitioning));
                Assert.That(actual.MaxSizeInMegabytes, Is.EqualTo(size));
            });
        }

        async Task VerifyTopicPerEventTypeSubscription(string topicName, string subscriptionName, string queueName)
        {
            var actual = (await client.GetSubscriptionAsync(topicName, subscriptionName)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(actual.LockDuration, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(actual.ForwardTo.EndsWith($"/{queueName}", StringComparison.Ordinal), Is.True);
                Assert.That(actual.EnableDeadLetteringOnFilterEvaluationExceptions, Is.EqualTo(false));
                Assert.That(actual.MaxDeliveryCount, Is.EqualTo(int.MaxValue));
                Assert.That(actual.EnableBatchedOperations, Is.EqualTo(true));
                Assert.That(actual.UserMetadata, Is.EqualTo(queueName));
            });
        }

        async Task VerifySubscriptionDoesNotExist(string topicName, string subscriptionName)
        {
            var exists = (await client.SubscriptionExistsAsync(topicName, subscriptionName)).Value;
            Assert.That(exists, Is.False);
        }

        async Task VerifySingleTopicSubscription(string topicName, string subscriptionName, string queueName)
        {
            var actual = (await client.GetSubscriptionAsync(topicName, subscriptionName)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(actual.LockDuration, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(actual.ForwardTo.EndsWith($"/{queueName}", StringComparison.Ordinal), Is.True);
                Assert.That(actual.EnableDeadLetteringOnFilterEvaluationExceptions, Is.EqualTo(false));
                Assert.That(actual.MaxDeliveryCount, Is.EqualTo(int.MaxValue));
                Assert.That(actual.EnableBatchedOperations, Is.EqualTo(true));
                Assert.That(actual.UserMetadata, Is.EqualTo(queueName));
            });

            // rules
            var rules = new List<RuleProperties>();

            await foreach (var rule in client.GetRulesAsync(topicName, subscriptionName))
            {
                rules.Add(rule);
            }

            Assert.That(rules, Has.Count.EqualTo(4));

            var defaultRule = rules[0];
            Assert.Multiple(() =>
            {
                Assert.That(defaultRule.Name, Is.EqualTo("$default"));
                Assert.That(((FalseRuleFilter)defaultRule.Filter).SqlExpression, Is.EqualTo(new FalseRuleFilter().SqlExpression));
            });

            var customRuleNameRule = rules[1];
            Assert.Multiple(() =>
            {
                Assert.That(customRuleNameRule.Name, Is.EqualTo("CustomRuleName"));
                Assert.That(((SqlRuleFilter)customRuleNameRule.Filter).SqlExpression, Is.EqualTo(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyNamespace1.MyMessage3%'").SqlExpression));
            });

            var myMessage1Rule = rules[2];
            Assert.Multiple(() =>
            {
                Assert.That(myMessage1Rule.Name, Is.EqualTo("MyMessage1"));
                Assert.That(((SqlRuleFilter)myMessage1Rule.Filter).SqlExpression, Is.EqualTo(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyMessage1%'").SqlExpression));
            });

            var myMessage2WithNamespace = rules[3];
            Assert.Multiple(() =>
            {
                Assert.That(myMessage2WithNamespace.Name, Is.EqualTo("MyNamespace1.MyMessage2"));
                Assert.That(((SqlRuleFilter)myMessage2WithNamespace.Filter).SqlExpression, Is.EqualTo(new SqlRuleFilter("[NServiceBus.EnclosedMessageTypes] LIKE '%MyNamespace1.MyMessage2%'").SqlExpression));
            });
        }

        async Task VerifySubscriptionContainsOnlyDefaultRejectAllRule(string topicName, string subscriptionName)
        {
            var rules = new List<RuleProperties>();

            await foreach (var rule in client.GetRulesAsync(topicName, subscriptionName))
            {
                rules.Add(rule);
            }

            Assert.That(rules, Has.Count.EqualTo(1));

            var defaultRule = rules[0];
            Assert.Multiple(() =>
            {
                Assert.That(defaultRule.Name, Is.EqualTo("$default"));
                Assert.That(((FalseRuleFilter)defaultRule.Filter).SqlExpression, Is.EqualTo(new FalseRuleFilter().SqlExpression));
            });
        }

        async Task VerifySubscriptionContainsOnlyDefaultMatchAllRule(string topicName, string subscriptionName)
        {
            var rules = new List<RuleProperties>();

            await foreach (var rule in client.GetRulesAsync(topicName, subscriptionName))
            {
                rules.Add(rule);
            }

            Assert.That(rules, Has.Count.EqualTo(1));

            var defaultRule = rules[0];
            Assert.Multiple(() =>
            {
                Assert.That(defaultRule.Name, Is.EqualTo("$default"));
                Assert.That(((TrueRuleFilter)defaultRule.Filter).SqlExpression, Is.EqualTo(new TrueRuleFilter().SqlExpression));
            });
        }

        async Task VerifyQueueExists(bool queueShouldExist)
        {
            var queueExists = (await client.QueueExistsAsync(QueueName)).Value;
            Assert.That(queueExists, Is.EqualTo(queueShouldExist));
        }

        static async Task<(string output, string error, int exitCode)> Execute(string command)
        {
            var frameworkVersion = RuntimeInformation.FrameworkDescription.AsSpan()[5..];

            var process = new Process
            {
                StartInfo =
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = TestContext.CurrentContext.TestDirectory,
                    FileName = "dotnet",
                    Arguments = $"--fx-version {frameworkVersion} NServiceBus.Transport.AzureServiceBus.CommandLine.dll " + command
                }
            };

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
