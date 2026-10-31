namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Logging;

// Remembers topics that returned MessagingEntityNotFound so that publishes to them can be skipped until they exist.
// Every failed link open makes the service delay the next authorization (put-token) on the same connection by
// about a second more, up to about five seconds, for any entity. The delay is per connection, so the topics are
// checked again in the background, on a connection that nothing else uses.
sealed class MissingTopicTracker(Func<string, CancellationToken, Task<bool>> topicExists, TimeSpan probeInterval) : IAsyncDisposable
{
    public static readonly TimeSpan DefaultProbeInterval = TimeSpan.FromSeconds(30);

    public TimeSpan ProbeInterval { get; } = probeInterval;

    public bool IsMissing(string topic) => !probes.IsEmpty && probes.ContainsKey(topic);

    // Returns true when the topic wasn't known to be missing, so that the caller warns only once per topic.
    public bool MarkMissing(string topic)
    {
        var probe = new Probe();
        if (stopping.IsCancellationRequested || !probes.TryAdd(topic, probe))
        {
            return false;
        }

        probe.Loop = Task.Run(() => ProbeUntilTopicExists(topic, probe, stopping.Token));
        return true;
    }

    // Returns true when the topic was known to be missing.
    public bool MarkPresent(string topic) => !probes.IsEmpty && probes.TryRemove(topic, out _);

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(probes.Values.Select(p => p.Loop ?? Task.CompletedTask)).ConfigureAwait(false);
        stopping.Dispose();
    }

    // Creates a probe that opens a send link to the topic without sending anything.
    public static Func<string, CancellationToken, Task<bool>> UsingSendLink(Func<ServiceBusClient> probeClient) =>
        async (topic, cancellationToken) =>
        {
            try
            {
                var sender = probeClient().CreateSender(topic);
                await using (sender.ConfigureAwait(false))
                {
                    using var batch = await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                }
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                return false;
            }
        };

    async Task ProbeUntilTopicExists(string topic, Probe probe, CancellationToken cancellationToken)
    {
        while (probes.TryGetValue(topic, out var current) && current == probe)
        {
            try
            {
                await Task.Delay(ProbeInterval, cancellationToken).ConfigureAwait(false);

                if (await topicExists(topic, cancellationToken).ConfigureAwait(false))
                {
                    if (probes.TryRemove(KeyValuePair.Create(topic, probe)) && Log.IsInfoEnabled)
                    {
                        Log.Info($"Topic {topic} exists now. Publishing to it is no longer skipped.");
                    }

                    return;
                }

                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Topic {topic} still does not exist. Checking again in {ProbeInterval.TotalSeconds:0} seconds.");
                }
            }
            catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
            {
                return;
            }
            catch (Exception ex)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Checking whether topic {topic} exists failed. Checking again in {ProbeInterval.TotalSeconds:0} seconds.", ex);
                }
            }
        }
    }

    sealed class Probe
    {
        public Task? Loop;
    }

    readonly ConcurrentDictionary<string, Probe> probes = new(StringComparer.OrdinalIgnoreCase);
    readonly CancellationTokenSource stopping = new();

    static readonly ILog Log = LogManager.GetLogger<MissingTopicTracker>();
}