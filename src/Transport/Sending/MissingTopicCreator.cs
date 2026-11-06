namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Logging;

// Coordinates topic creation and delivery recovery. A completed creation does not guarantee that an existing
// AMQP connection sees the topic immediately, so post-creation 404s are retried instead of swallowed.
sealed class MissingTopicCreator(
    Func<string, CancellationToken, Task> createTopic,
    bool enabled,
    TimeSpan recoveryTimeout,
    TimeSpan retryDelay) : IAsyncDisposable
{
    public static readonly TimeSpan DefaultRecoveryTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);
    public const int MaxDeliveryAttemptsAfterCreation = 5;

    public MissingTopicCreator(Func<string, CancellationToken, Task> createTopic, bool enabled)
        : this(createTopic, enabled, DefaultRecoveryTimeout, DefaultRetryDelay)
    {
    }

    public Task ExecuteAsync(string topic, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteAsync(topic, async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(string topic, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A publish that arrives during creation waits for the same creation task, then attempts its own delivery.
        if (enabled && creations.TryGetValue(topic, out var pending) && (!pending.IsValueCreated || !pending.Value.IsCompleted))
        {
            return await RecoverDelivery(topic, pending, operation, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException e) when (enabled && e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            return await RecoverDelivery(topic, GetCreation(topic), operation, cancellationToken).ConfigureAwait(false);
        }
    }

    Lazy<Task> GetCreation(string topic)
    {
        stopping.Token.ThrowIfCancellationRequested();
        var shutdownToken = stopping.Token;
        var candidate = new Lazy<Task>(() => CreateTopic(topic, shutdownToken), LazyThreadSafetyMode.ExecutionAndPublication);

        // The dictionary factory has no side effects. Only the selected Lazy starts the management call.
        // A later publish that finds the topic missing may start a new creation; there is no recreation cooldown.
        return creations.AddOrUpdate(topic, candidate,
            (_, existing) => existing.IsValueCreated && existing.Value.IsCompleted ? candidate : existing);
    }

    async Task<T> RecoverDelivery<T>(string topic, Lazy<Task> creation, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var recovery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopping.Token);
        recovery.CancelAfter(recoveryTimeout);
        var recoveryToken = recovery.Token;
        ServiceBusException? lastFailure = null;

        try
        {
            // Cancelling one caller stops its wait, not the shared creation used by other publishes.
            await creation.Value.WaitAsync(recoveryToken).ConfigureAwait(false);

            for (var attempt = 0; attempt < MaxDeliveryAttemptsAfterCreation; attempt++)
            {
                recoveryToken.ThrowIfCancellationRequested();
                try
                {
                    return await operation(recoveryToken).WaitAsync(recoveryToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    lastFailure = e;
                }

                if (attempt + 1 < MaxDeliveryAttemptsAfterCreation)
                {
                    await Task.Delay(retryDelay, recoveryToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex.IsCausedBy(recoveryToken) && !cancellationToken.IsCancellationRequested && !stopping.IsCancellationRequested)
        {
            throw new TimeoutException($"Publishing to topic {topic} did not recover within {recoveryTimeout.TotalSeconds:0.###} seconds while creating the topic and waiting for it to become available.", lastFailure);
        }
        finally
        {
            if (creation.IsValueCreated && creation.Value.IsFaulted)
            {
                // All current awaiters observe the failure. A later publish may try provisioning again.
                creations.TryRemove(KeyValuePair.Create(topic, creation));
            }
        }

        throw new TimeoutException($"Publishing to topic {topic} still failed after {MaxDeliveryAttemptsAfterCreation} delivery attempts following topic creation.", lastFailure);
    }

    async Task CreateTopic(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await createTopic(topic, cancellationToken).ConfigureAwait(false);

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} because publishing to it found it missing. Pending publishes will now attempt delivery. Events published before a subscription exists are not delivered.");
            }
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Another publisher or subscriber has provisioned the topic. Still attempt the pending delivery.
            return;
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            // Distinguish provisioning failures from a missing destination that the dispatcher normally swallows.
            throw new InvalidOperationException($"Creating topic {topic} before retrying the publish failed.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        var pending = creations.Values.Where(c => c.IsValueCreated && !c.Value.IsCompleted).Select(c => c.Value).ToArray();
        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Shutdown cancels the shared provisioning calls.
            return;
        }
        finally
        {
            stopping.Dispose();
        }
    }

    readonly ConcurrentDictionary<string, Lazy<Task>> creations = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stopping = new();
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}