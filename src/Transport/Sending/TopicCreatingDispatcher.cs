namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using EventRouting;
using Logging;

// Checks and provisions unconfirmed topics before the first AMQP attempt. The inner dispatcher must throw on missing topics.
// Unconfirmed topics are set up separately so other destinations can dispatch while their setup is pending.
// Topics stay confirmed for the lifetime of the endpoint, so topics deleted while it runs are not recreated.
sealed class TopicCreatingDispatcher(
    IMessageDispatcher inner,
    TopicTopology topology,
    DestinationManager destinationManager,
    Func<string, CancellationToken, Task<bool>> topicExists,
    Func<string, CancellationToken, Task> createTopic,
    TopicAdministrationLimiter administrationLimiter,
    TimeSpan recoveryTimeout,
    TimeProvider timeProvider) : IMessageDispatcher
{
    public static readonly TimeSpan DefaultRecoveryTimeout = TimeSpan.FromSeconds(30);
    public TopicCreatingDispatcher(IMessageDispatcher inner, TopicTopology topology, DestinationManager destinationManager,
        Func<string, CancellationToken, Task<bool>> topicExists, Func<string, CancellationToken, Task> createTopic)
        : this(inner, topology, destinationManager, topicExists, createTopic,
            new TopicAdministrationLimiter(TopicAdministrationLimiter.DefaultMinimumInterval,
                TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * TopicAdministrationLimiter.MaximumStartupDelay.TotalMilliseconds), TimeProvider.System),
            DefaultRecoveryTimeout, TimeProvider.System)
    {
    }

    public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
    {
        Dictionary<string, TransportOperations>? unconfirmed = null;
        foreach (var operation in outgoingMessages.MulticastTransportOperations)
        {
            var topic = operation.ExtractDestination(topology, destinationManager);
            if (!IsConfirmed(topic))
            {
                unconfirmed ??= new Dictionary<string, TransportOperations>(StringComparer.OrdinalIgnoreCase);
                if (!unconfirmed.TryGetValue(topic, out var operations))
                {
                    unconfirmed[topic] = operations = new TransportOperations();
                }

                operations.MulticastTransportOperations.Add(operation);
            }
        }

        // Splitting would hide the transaction limit that the inner dispatcher reports.
        if (unconfirmed is null || ExceedsTransactionLimit(outgoingMessages, transaction))
        {
            return DispatchToConfirmedTopics(outgoingMessages, transaction, cancellationToken);
        }

        var rest = new TransportOperations();
        rest.UnicastTransportOperations.AddRange(outgoingMessages.UnicastTransportOperations);
        foreach (var operation in outgoingMessages.MulticastTransportOperations)
        {
            if (!unconfirmed.ContainsKey(operation.ExtractDestination(topology, destinationManager)))
            {
                rest.MulticastTransportOperations.Add(operation);
            }
        }

        var dispatches = new List<Task>(unconfirmed.Count + 1);
        if (rest.UnicastTransportOperations.Count > 0 || rest.MulticastTransportOperations.Count > 0)
        {
            dispatches.Add(DispatchToConfirmedTopics(rest, transaction, cancellationToken));
        }

        foreach (var (topic, operations) in unconfirmed)
        {
            dispatches.Add(DispatchToUnconfirmedTopic(topic, operations, transaction, cancellationToken));
        }

        return Task.WhenAll(dispatches);
    }

    Task DispatchToConfirmedTopics(TransportOperations operations, TransportTransaction transaction, CancellationToken cancellationToken) =>
        inner.Dispatch(operations, transaction, cancellationToken);

    async Task DispatchToUnconfirmedTopic(string topic, TransportOperations operations, TransportTransaction transaction, CancellationToken cancellationToken)
    {
        var gate = gates.GetOrAdd(topic, static _ => new Gate());
        var started = timeProvider.GetTimestamp();
        using var deadline = new CancellationTokenSource(recoveryTimeout, timeProvider);
        using var recovery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var recoveryToken = recovery.Token;
        var held = false;
        try
        {
            await gate.Semaphore.WaitAsync(recoveryToken).ConfigureAwait(false);
            held = true;
            if (timeProvider.GetElapsedTime(started) >= recoveryTimeout)
            {
                throw RecoveryTimedOut(topic, null);
            }
            if (gate.Confirmed)
            {
                gate.Semaphore.Release();
                held = false;
                await DispatchToConfirmedTopics(operations, transaction, recoveryToken).WaitAsync(recoveryToken).ConfigureAwait(false);
                return;
            }

            await EnsureTopicExists(topic, recoveryToken).ConfigureAwait(false);
            await inner.Dispatch(operations, transaction, recoveryToken).WaitAsync(recoveryToken).ConfigureAwait(false);
            gate.Confirmed = true;
        }
        catch (Exception ex) when (ex.IsCausedBy(recoveryToken) && !cancellationToken.IsCancellationRequested)
        {
            throw RecoveryTimedOut(topic, ex);
        }
        finally
        {
            if (held)
            {
                gate.Semaphore.Release();
            }
        }
    }

    async Task EnsureTopicExists(string topic, CancellationToken cancellationToken)
    {
        // Exceptions from the check are failures, not a negative existence result.
        var exists = await administrationLimiter.ExecuteAsync(_ => topicExists(topic, cancellationToken), cancellationToken)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            await CreateTopic(topic, cancellationToken).ConfigureAwait(false);
        }
    }

    async Task CreateTopic(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await administrationLimiter.ExecuteAsync(_ => createTopic(topic, cancellationToken), cancellationToken)
                .WaitAsync(cancellationToken).ConfigureAwait(false);

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

    TimeoutException RecoveryTimedOut(string topic, Exception? innerException) =>
        new($"Publishing to topic {topic} did not recover within {recoveryTimeout.TotalSeconds:0.###} seconds while waiting for topic admission, setup, and delivery.", innerException);

    bool IsConfirmed(string topic) => gates.TryGetValue(topic, out var gate) && gate.Confirmed;

    static bool ExceedsTransactionLimit(TransportOperations outgoingMessages, TransportTransaction transaction)
    {
        if (!transaction.TryGet<AzureServiceBusTransportTransaction>(out var azureServiceBusTransaction) || !azureServiceBusTransaction.HasTransaction)
        {
            return false;
        }

        var count = 0;
        foreach (var operation in outgoingMessages.UnicastTransportOperations)
        {
            count += operation.RequiredDispatchConsistency == DispatchConsistency.Default ? 1 : 0;
        }
        foreach (var operation in outgoingMessages.MulticastTransportOperations)
        {
            count += operation.RequiredDispatchConsistency == DispatchConsistency.Default ? 1 : 0;
        }
        return count > MessageDispatcher.MaxMessageThresholdForTransaction;
    }

    sealed class Gate
    {
        public readonly SemaphoreSlim Semaphore = new(1);
        public volatile bool Confirmed;
    }

    readonly ConcurrentDictionary<string, Gate> gates = new(StringComparer.OrdinalIgnoreCase);
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}