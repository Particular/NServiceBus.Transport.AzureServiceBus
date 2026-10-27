namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Extensibility;
using NServiceBus.Transport.AzureServiceBus.EventRouting;
using NUnit.Framework;
using Unicast.Messages;

[TestFixture]
public class TopicPerEventSubscriptionManagerLeastPrivilegeTests
{
    [TestCaseSource(nameof(ProbeFailuresMeaningTheSubscriptionIsMissing))]
    public void Should_rethrow_when_probe_cannot_confirm_the_subscription(Exception probeFailure)
    {
        var subscriptionManager = CreateSubscriptionManager(probeFailure);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new ContextBag()));
    }

    [Test]
    public async Task Should_continue_when_probe_finds_the_subscription()
    {
        var subscriptionManager = CreateSubscriptionManager(probeFailure: null);

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new ContextBag());
    }

    [Test]
    public async Task Should_continue_when_probe_is_rejected_because_the_subscription_forwards()
    {
        var subscriptionManager = CreateSubscriptionManager(new InvalidOperationException("Cannot create a message browser on an entity with auto-forwarding enabled."));

        await subscriptionManager.SubscribeAll([new MessageMetadata(typeof(MyEvent))], new ContextBag());
    }

    static IEnumerable<TestCaseData> ProbeFailuresMeaningTheSubscriptionIsMissing()
    {
        yield return new TestCaseData(new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound)).SetArgDisplayNames("not found");
        yield return new TestCaseData(new ServiceBusException("timeout", ServiceBusFailureReason.ServiceTimeout)).SetArgDisplayNames("service timeout");
        yield return new TestCaseData(new UnauthorizedAccessException("no listen rights")).SetArgDisplayNames("unauthorized");
    }

    static TopicPerEventTopologySubscriptionManager CreateSubscriptionManager(Exception probeFailure) =>
        new(new SubscriptionManagerCreationOptions
        {
            SubscribingQueueName = "SubscribingQueue",
            Client = new ProbingServiceBusClient(probeFailure),
            AdministrationClient = new UnauthorizedAdministrationClient(),
            CriticalErrorAction = (_, _, _) => { }
        }, new TopologyOptions(), new StartupDiagnosticEntries());

    class UnauthorizedAdministrationClient : ServiceBusAdministrationClient
    {
        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException("no manage rights");
    }

    class ProbingServiceBusClient(Exception probeFailure) : ServiceBusClient
    {
        public override ServiceBusRuleManager CreateRuleManager(string topicName, string subscriptionName) => new ProbingRuleManager(probeFailure);

        class ProbingRuleManager(Exception probeFailure) : ServiceBusRuleManager
        {
            public override async IAsyncEnumerable<RuleProperties> GetRulesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.Yield();

                if (probeFailure != null)
                {
                    throw probeFailure;
                }

                yield break;
            }

            public override Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    class MyEvent;
}
