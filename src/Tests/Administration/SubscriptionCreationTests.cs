namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Extensibility;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class SubscriptionCreationTests
{
    [TestCaseSource(nameof(EntityNotVisibleYetFailures))]
    public async Task Should_retry_while_the_topic_or_forwarding_destination_is_not_visible_yet(Exception failure)
    {
        var attempts = 0;

        await SubscriptionCreation.CreateWithRetries(Forwarding(), FastRetries(maxRetries: 3), _ =>
            ++attempts < 3 ? Task.FromException(failure) : Task.CompletedTask);

        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void Should_not_retry_bad_requests_when_the_subscription_does_not_forward()
    {
        var attempts = 0;

        Assert.ThrowsAsync<ArgumentException>(() => SubscriptionCreation.CreateWithRetries(new CreateSubscriptionOptions("topic", "subscription"), FastRetries(maxRetries: 3), _ =>
        {
            attempts++;
            return Task.FromException(ForwardingDestinationMissing());
        }));

        Assert.That(attempts, Is.EqualTo(1));
    }

    [TestCaseSource(nameof(OtherFailures))]
    public void Should_not_retry_other_failures(Exception failure)
    {
        var attempts = 0;

        var thrown = Assert.CatchAsync(() => SubscriptionCreation.CreateWithRetries(Forwarding(), FastRetries(maxRetries: 3), _ =>
        {
            attempts++;
            return Task.FromException(failure);
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(attempts, Is.EqualTo(1));
        }
    }

    [TestCase(0)]
    [TestCase(2)]
    public void Should_give_up_after_the_configured_number_of_retries(int maxRetries)
    {
        var attempts = 0;
        var failure = TopicNotVisibleYet();

        var thrown = Assert.CatchAsync(() => SubscriptionCreation.CreateWithRetries(Forwarding(), FastRetries(maxRetries), _ =>
        {
            attempts++;
            return Task.FromException(failure);
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(attempts, Is.EqualTo(maxRetries + 1));
        }
    }

    [Test]
    public void Should_stop_retrying_when_cancelled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var attempts = 0;

        Assert.CatchAsync<OperationCanceledException>(() => SubscriptionCreation.CreateWithRetries(Forwarding(),
            new ServiceBusRetryOptions { Delay = TimeSpan.FromMinutes(1), MaxRetries = 3 }, _ =>
            {
                attempts++;
                cancellationTokenSource.Cancel();
                return Task.FromException(TopicNotVisibleYet());
            }, cancellationTokenSource.Token));

        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_retry_catch_all_subscriptions_of_the_topic_per_event_topology()
    {
        var administrationClient = new SubscriptionNotYetCreatableAdministrationClient(TopicNotVisibleYet(), failures: 1);
        var subscriptionManager = new TopicPerEventTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(),
            AdministrationClient = administrationClient,
            SubscriptionCreationRetryOptions = FastRetries(maxRetries: 3)
        }, new TopologyOptions(), new StartupDiagnosticEntries());

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new ContextBag());

        Assert.That(administrationClient.SubscriptionAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_retry_the_subscription_of_the_migration_topology()
    {
        var administrationClient = new SubscriptionNotYetCreatableAdministrationClient(TopicNotVisibleYet(), failures: 1);
#pragma warning disable CS0618 // Type or member is obsolete
        var subscriptionManager = new MigrationTopologySubscriptionManager(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new RecordingServiceBusClient(),
            AdministrationClient = administrationClient,
            SetupInfrastructure = true,
            SubscriptionCreationRetryOptions = FastRetries(maxRetries: 3)
        }, new MigrationTopologyOptions { TopicToPublishTo = "PublishTopic", TopicToSubscribeOn = "SubscribeTopic" }, new StartupDiagnosticEntries());
#pragma warning restore CS0618 // Type or member is obsolete

        await subscriptionManager.SetupInfrastructureIfNecessary();

        Assert.That(administrationClient.SubscriptionAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_retry_the_forwarding_subscription_of_the_migration_topic_hierarchy()
    {
        var administrationClient = new SubscriptionNotYetCreatableAdministrationClient(ForwardingDestinationMissing(), failures: 1);
#pragma warning disable CS0618 // Type or member is obsolete
        var transportSettings = new AzureServiceBusTransport("connection-string", TopicTopology.MigrateFromTopicHierarchy("bundle-1", "bundle-2"));
#pragma warning restore CS0618 // Type or member is obsolete

        await new MigrationTopologyCreator(transportSettings, FastRetries(maxRetries: 3)).Create(administrationClient);

        Assert.That(administrationClient.SubscriptionAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_retry_regardless_of_the_messaging_retry_policy()
    {
        var administrationClient = new SubscriptionNotYetCreatableAdministrationClient(ForwardingDestinationMissing(), failures: 1);
#pragma warning disable CS0618 // Type or member is obsolete
        var transportSettings = new AzureServiceBusTransport("connection-string", TopicTopology.MigrateFromTopicHierarchy("bundle-1", "bundle-2"))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            RetryPolicyOptions = new ServiceBusRetryOptions { MaxRetries = 0 }
        };

        await new MigrationTopologyCreator(transportSettings).Create(administrationClient);

        Assert.That(administrationClient.SubscriptionAttempts, Is.EqualTo(2));
    }

    static IEnumerable<TestCaseData> EntityNotVisibleYetFailures()
    {
        yield return new TestCaseData(TopicNotVisibleYet()).SetArgDisplayNames("topic not found");
        yield return new TestCaseData(ForwardingDestinationMissing()).SetArgDisplayNames("forwarding destination missing");
    }

    static IEnumerable<TestCaseData> OtherFailures()
    {
        yield return new TestCaseData(new ServiceBusException("exists", ServiceBusFailureReason.MessagingEntityAlreadyExists)).SetArgDisplayNames("already exists");
        yield return new TestCaseData(new UnauthorizedAccessException("no manage rights")).SetArgDisplayNames("unauthorized");
        yield return new TestCaseData(new ArgumentException("invalid", new RequestFailedException(409, "conflict"))).SetArgDisplayNames("other status");
        yield return new TestCaseData(new ArgumentException("invalid")).SetArgDisplayNames("argument without response");
    }

    static ServiceBusException TopicNotVisibleYet() =>
        new("not found", ServiceBusFailureReason.MessagingEntityNotFound, innerException: new RequestFailedException(404, "not found"));

    static ArgumentException ForwardingDestinationMissing() =>
        new("bad request", new RequestFailedException(400, "bad request"));

    static CreateSubscriptionOptions Forwarding() => new("topic", "subscription") { ForwardTo = "queue" };

    static ServiceBusRetryOptions FastRetries(int maxRetries) => new() { Delay = TimeSpan.FromMilliseconds(1), MaxRetries = maxRetries };

    class SubscriptionNotYetCreatableAdministrationClient(Exception failure, int failures) : ServiceBusAdministrationClient
    {
        public int SubscriptionAttempts => subscriptionAttempts;

        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CancellationToken cancellationToken = default) =>
            AttemptSubscription();

        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions rule, CancellationToken cancellationToken = default) =>
            AttemptSubscription();

        public override Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult<Response<TopicProperties>>(null);

        Task<Response<SubscriptionProperties>> AttemptSubscription() =>
            Interlocked.Increment(ref subscriptionAttempts) <= failures
                ? Task.FromException<Response<SubscriptionProperties>>(failure)
                : Task.FromResult<Response<SubscriptionProperties>>(null);

        int subscriptionAttempts;
    }

    class MyEvent;
}