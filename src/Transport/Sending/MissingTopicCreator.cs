namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Logging;

// Creates a topic in the background when a publish finds it missing. Every failed link open makes the service
// delay the next authorization on the same connection, so once the topic exists publishing to it is fast again.
// A topic that was just created has no subscriptions, so the publish that found it missing isn't retried.
sealed class MissingTopicCreator(Func<string, CancellationToken, Task> createTopic, bool enabled, TimeSpan? gracePeriod = null, TimeProvider? timeProvider = null) : IAsyncDisposable
{
    // After a 404, Azure Service Bus keeps reporting the topic missing on the same connection for several seconds
    // after it's created. Publishing in that period would only add failures, and with them more delay, to the connection.
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(15);

    public TimeSpan GracePeriod { get; } = gracePeriod ?? DefaultGracePeriod;

    // Publishes are skipped while the topic is being created and during the grace period after it was created.
    // Events published then have nowhere to go anyway, because the new topic has no subscriptions yet.
    public bool ShouldSkip(string topic) => enabled && !creations.IsEmpty && (IsCreating(topic) || IsInGracePeriod(topic));

    public bool IsCreating(string topic) => creations.TryGetValue(topic, out var creation) && creation.CreatedAt is null && !creation.Task.IsCompleted;

    public bool IsInGracePeriod(string topic) =>
        creations.TryGetValue(topic, out var creation) && creation.CreatedAt is { } createdAt && time.GetUtcNow() < createdAt + GracePeriod;

    // Returns true when the topic is being created or was created, so that the caller doesn't need to warn.
    public bool TryCreate(string topic)
    {
        if (!enabled || stopping.IsCancellationRequested || failed.ContainsKey(topic))
        {
            return false;
        }

        // One creation per topic at a time. A topic that is missing again after its grace period was deleted since, so it's created again.
        _ = creations.AddOrUpdate(topic,
            static (key, creator) => creator.Start(key),
            static (key, existing, creator) => existing.CreatedAt is not null && !creator.IsInGracePeriod(key) ? creator.Start(key) : existing,
            this);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(creations.Values.Select(c => c.Task).ToArray()).ConfigureAwait(false);
        stopping.Dispose();
    }

    Creation Start(string topic)
    {
        var creation = new Creation();
        var cancellationToken = stopping.Token;
        creation.Task = Task.Run(() => CreateTopic(topic, creation, cancellationToken), CancellationToken.None);
        return creation;
    }

    async Task CreateTopic(string topic, Creation creation, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await createTopic(topic, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                creation.CreatedAt = time.GetUtcNow();
                return;
            }

            creation.CreatedAt = time.GetUtcNow();

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} because publishing to it found it missing. Publishes to it are skipped for the next {GracePeriod.TotalSeconds:0} seconds, while Azure Service Bus may still report it missing. Events published to it before a subscriber adds a subscription are not delivered.");
            }
        }
        catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
        {
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

    sealed class Creation
    {
        public Task Task = Task.CompletedTask;

        public DateTimeOffset? CreatedAt
        {
            get
            {
                var ticks = Volatile.Read(ref createdAtTicks);
                return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            set => Volatile.Write(ref createdAtTicks, value?.UtcTicks ?? 0);
        }

        long createdAtTicks;
    }

    readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    readonly ConcurrentDictionary<string, Creation> creations = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, bool> failed = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stopping = new();

    // Logs under the dispatcher's logger because that's the one users already filter on for missing topics
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}