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
sealed class MissingTopicCreator(Func<string, CancellationToken, Task> createTopic, bool enabled) : IAsyncDisposable
{
    // Returns true when the topic is being created or was created, so that the caller doesn't need to warn.
    public bool TryCreate(string topic)
    {
        if (!enabled || stopping.IsCancellationRequested || failed.ContainsKey(topic))
        {
            return false;
        }

        // One creation per topic at a time. A completed creation is started again, because the topic was deleted since.
        _ = creations.AddOrUpdate(topic,
            static (key, creator) => creator.Create(key, creator.stopping.Token),
            static (key, existing, creator) => existing.IsCompletedSuccessfully ? creator.Create(key, creator.stopping.Token) : existing,
            this);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(creations.Values.ToArray()).ConfigureAwait(false);
        stopping.Dispose();
    }

    Task Create(string topic, CancellationToken cancellationToken) => Task.Run(() => CreateTopic(topic, cancellationToken), CancellationToken.None);

    async Task CreateTopic(string topic, CancellationToken cancellationToken)
    {
        try
        {
            await createTopic(topic, cancellationToken).ConfigureAwait(false);

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} because publishing to it found it missing. Events published to it before a subscriber adds a subscription are not delivered.");
            }
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
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

    readonly ConcurrentDictionary<string, Task> creations = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, bool> failed = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stopping = new();

    // Logs under the dispatcher's logger because that's the one users already filter on for missing topics
    static readonly ILog Log = LogManager.GetLogger<MessageDispatcher>();
}