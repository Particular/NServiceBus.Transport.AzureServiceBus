namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using EventRouting;
using Logging;

// Checks and provisions unconfirmed topics before dispatching the original operations once. The inner dispatcher must throw on missing topics.
// Nothing is sent until every unconfirmed topic in the call is set up, so a setup failure leaves no partial dispatch behind.
// Topics stay confirmed for the lifetime of the endpoint, so topics deleted while it runs are not recreated.
sealed class TopicCreatingDispatcher(
    IMessageDispatcher inner,
    TopicTopology topology,
    DestinationManager destinationManager,
    Func<string, CancellationToken, Task<bool>> topicExists,
    Func<string, CancellationToken, Task> createTopic,
    TopicAdministrationLimiter administrationLimiter) : IMessageDispatcher
{
    public TopicCreatingDispatcher(IMessageDispatcher inner, TopicTopology topology, DestinationManager destinationManager,
        Func<string, CancellationToken, Task<bool>> topicExists, Func<string, CancellationToken, Task> createTopic)
        : this(inner, topology, destinationManager, topicExists, createTopic,
            new TopicAdministrationLimiter(TopicAdministrationLimiter.DefaultMinimumInterval,
                TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * TopicAdministrationLimiter.MaximumStartupDelay.TotalMilliseconds), TimeProvider.System))
    {
    }

    public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
    {
        HashSet<string>? unconfirmed = null;
        foreach (var operation in outgoingMessages.MulticastTransportOperations)
        {
            var topic = operation.ExtractDestination(topology, destinationManager);
            if (!confirmed.ContainsKey(topic))
            {
                unconfirmed ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                unconfirmed.Add(topic);
            }
        }

        return unconfirmed is null
            ? inner.Dispatch(outgoingMessages, transaction, cancellationToken)
            : DispatchAfterSetup(unconfirmed, outgoingMessages, transaction, cancellationToken);
    }

    async Task DispatchAfterSetup(HashSet<string> topics, TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
    {
        var pendingSetups = new List<Task>(topics.Count);
        foreach (var topic in topics)
        {
            // Shared between callers, so no caller's token cancels it for the others. The SDK's own timeouts bound it.
            pendingSetups.Add(setups.GetOrAdd(topic, static (topic, self) => new Lazy<Task>(() => self.SetUp(topic, CancellationToken.None)), this).Value);
        }

        await Task.WhenAll(pendingSetups).WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await inner.Dispatch(outgoingMessages, transaction, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // The SDK doesn't identify the missing entity. Check these topics again on the caller's next attempt.
            foreach (var topic in topics)
            {
                setups.TryRemove(topic, out _);
            }

            throw;
        }

        foreach (var topic in topics)
        {
            confirmed.TryAdd(topic, 0);
        }
    }

    async Task SetUp(string topic, CancellationToken cancellationToken)
    {
        var succeeded = false;
        try
        {
            // Exceptions from the check are failures, not a negative existence result.
            var exists = await administrationLimiter.ExecuteAsync(_ => topicExists(topic, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                await CreateTopic(topic, cancellationToken).ConfigureAwait(false);
            }

            succeeded = true;
        }
        finally
        {
            // Removed here rather than by the callers, which may have stopped waiting, so a failure is never cached
            if (!succeeded)
            {
                setups.TryRemove(topic, out _);
            }
        }
    }

    async Task CreateTopic(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await administrationLimiter.ExecuteAsync(_ => createTopic(topic, cancellationToken), cancellationToken).ConfigureAwait(false);

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} before publishing to it. Events published before a subscription exists are not delivered.");
            }
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Another publisher or a subscriber created it. Still attempt delivery.
            return;
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            throw new InvalidOperationException($"Creating topic {topic} before publishing failed.", ex);
        }
    }

    readonly ConcurrentDictionary<string, Lazy<Task>> setups = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, byte> confirmed = new(StringComparer.OrdinalIgnoreCase);
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}