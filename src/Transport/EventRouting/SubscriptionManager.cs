namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Extensibility;
using Unicast.Messages;

abstract class SubscriptionManager(
    SubscriptionManagerCreationOptions creationOptions)
    : ISubscriptionManager
{
    // Per topic and event, so that a successful subscribe does not clear the failures of other topics or of other events on the same topic.
    // Events without a topic are recorded with an empty topic.
    readonly ConcurrentDictionary<(string Topic, string EventType), string> subscribingFailures = new();

    protected SubscriptionManagerCreationOptions CreationOptions { get; } = creationOptions;

    public abstract Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default);

    public abstract Task Unsubscribe(MessageMetadata eventType, ContextBag context,
        CancellationToken cancellationToken = default);

    public ValueTask SetupInfrastructureIfNecessary(CancellationToken cancellationToken = default) =>
        CreationOptions.SetupInfrastructure ? SetupInfrastructureCore(cancellationToken) : default;

    protected virtual ValueTask SetupInfrastructureCore(CancellationToken cancellationToken = default) => default;

    // Core's auto-subscribe logs subscribe failures and carries on, so the receivers report them when they start
    public void ThrowIfSubscribingFailed()
    {
        if (!subscribingFailures.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(" ", subscribingFailures
                .OrderBy(f => f.Key.Topic, StringComparer.Ordinal)
                .ThenBy(f => f.Key.EventType, StringComparer.Ordinal)
                .Select(f => f.Value)
                .Distinct()));
        }
    }

    public virtual Task StartForwarders(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public abstract Task ShutdownForwarders(CancellationToken cancellationToken = default);

    protected void ThrowIfUnsubscribingIsNotSupported(string eventTypeFullName)
    {
        if (CreationOptions.AssertTopologyExists)
        {
            throw new InvalidOperationException($"AssertTopologyExists is enabled, so the endpoint cannot unsubscribe from '{eventTypeFullName}'. Remove the subscription or its filter rule outside the endpoint, for example with the asb-transport command-line tool.");
        }
    }

    // Records the result for every event on every checked topic and throws when any of them failed
    protected void RecordSubscribingResults(IReadOnlyCollection<(string Topic, string[] EventTypes, SubscribingFailure? Failure)> results)
    {
        foreach (var (topic, eventTypes, failure) in results)
        {
            foreach (var eventType in eventTypes)
            {
                if (failure is null)
                {
                    subscribingFailures.TryRemove((topic, eventType), out _);
                }
                else
                {
                    subscribingFailures[(topic, eventType)] = failure.Message;
                }
            }
        }

        var failures = results
            .Where(r => r.Failure is not null)
            .OrderBy(r => r.Topic, StringComparer.Ordinal)
            .Select(r => r.Failure!)
            .ToArray();

        if (failures.Length == 0)
        {
            return;
        }

        var exceptions = failures.Select(f => f.Exception).OfType<Exception>().ToArray();
        throw new InvalidOperationException(string.Join(" ", failures.Select(f => f.Message).Distinct()), exceptions.Length switch
        {
            0 => null,
            1 => exceptions[0],
            _ => new AggregateException(exceptions)
        });
    }

    // Checks one subscription and runs the follow-up only when the subscription exists with the endpoint's session setting
    protected async Task<SubscribingFailure?> AssertSubscription(string topicName, string subscriptionName, Func<CancellationToken, Task> whenExists, CancellationToken cancellationToken = default)
    {
        try
        {
            var requiresSession = await SubscriptionRequiresSession(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);

            if (requiresSession is null)
            {
                return new SubscribingFailure($"AssertTopologyExists is enabled, so subscription '{subscriptionName}' must exist on topic '{topicName}' before the endpoint subscribes, but it does not exist. Create the subscription, for example with the asb-transport command-line tool.");
            }

            if (requiresSession != CreationOptions.RequiresSession)
            {
                return new SubscribingFailure(requiresSession == true
                    ? $"Subscription '{subscriptionName}' on topic '{topicName}' requires sessions, but the endpoint does not have sessions enabled."
                    : $"Subscription '{subscriptionName}' on topic '{topicName}' does not require sessions, but the endpoint has sessions enabled.");
            }

            await whenExists(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            return new SubscribingFailure($"Subscribing through subscription '{subscriptionName}' on topic '{topicName}' failed: {ex.Message}", ex);
        }
    }

    // Accepting a session needs only Listen rights, unlike the administration client, and reports a missing subscription
    // immediately, unlike peeking or browsing rules (Azure/azure-sdk-for-net#64167). A new session ID has no messages, so nothing is received or locked.
    // Returns null when the subscription does not exist
    async Task<bool?> SubscriptionRequiresSession(string topicName, string subscriptionName, CancellationToken cancellationToken)
    {
        try
        {
            var receiver = await CreationOptions.Client.AcceptSessionAsync(topicName, subscriptionName, Guid.NewGuid().ToString(), cancellationToken: cancellationToken).ConfigureAwait(false);
            await receiver.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        // The SDK reports an existing subscription that does not require sessions this way
        catch (InvalidOperationException)
        {
            return false;
        }
        // Also reported when the topic does not exist
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            return null;
        }
    }

    protected sealed record SubscribingFailure(string Message, Exception? Exception = null);
}
