namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Logging;

// Creates a topic in the background when a publish finds it missing. The publish that failed isn't retried.
// A successful create doesn't guarantee that the publisher's existing connection immediately sees the topic.
sealed class MissingTopicCreator(Func<string, CancellationToken, Task> createTopic, bool enabled) : IAsyncDisposable
{
    // Resume attempting publishes as soon as creation completes, so that a subscriber that has become ready can receive them.
    public bool ShouldSkip(string topic) => enabled && !creations.IsEmpty && IsCreating(topic);

    public bool IsCreating(string topic) => creations.TryGetValue(topic, out var creation) && !creation.IsCompleted;

    // Returns true when creation is underway or was started, so that the caller doesn't need to warn.
    public bool TryCreate(string topic)
    {
        if (!enabled || stopping.IsCancellationRequested || failed.ContainsKey(topic))
        {
            return false;
        }

        // A later 404 starts another creation attempt. It may indicate deletion or a stale service-side not-found result.
        _ = creations.AddOrUpdate(topic,
            static (key, creator) => creator.Start(key, creator.stopping.Token),
            static (key, existing, creator) => existing.IsCompletedSuccessfully ? creator.Start(key, creator.stopping.Token) : existing,
            this);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(creations.Values.ToArray()).ConfigureAwait(false);
        stopping.Dispose();
    }

    Task Start(string topic, CancellationToken cancellationToken) =>
        Task.Run(() => CreateTopic(topic, cancellationToken), CancellationToken.None);

    async Task CreateTopic(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await createTopic(topic, cancellationToken).ConfigureAwait(false);

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} because publishing to it found it missing. Events published before a subscription exists are not delivered.");
            }
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Another publisher or subscriber has already provisioned the topic.
            return;
        }
        catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
        {
            // Shutdown cancels the background creation.
            return;
        }
        catch (Exception ex)
        {
            // Not retried during this run, so that a missing right doesn't cause a management call on every publish
            failed[topic] = true;

            if (Log.IsWarnEnabled)
            {
                Log.Warn($"Creating topic {topic}, which publishing found missing, failed. Publishing to it keeps failing until the topic is created.", ex);
            }
        }
    }

    readonly ConcurrentDictionary<string, Task> creations = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, bool> failed = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stopping = new();

    // Logs under the dispatcher's logger because that's the one users already filter on for missing topics
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}