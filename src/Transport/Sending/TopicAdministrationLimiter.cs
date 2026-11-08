namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;

// Per-endpoint limit on publisher-side topic admin calls. HTTP retries aren't counted.
sealed class TopicAdministrationLimiter(TimeSpan minimumInterval, TimeSpan startupDelay, TimeProvider timeProvider)
{
    public static readonly TimeSpan DefaultMinimumInterval = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan MaximumStartupDelay = TimeSpan.FromSeconds(1);

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = started
                ? minimumInterval - timeProvider.GetElapsedTime(lastStarted)
                : startupDelay;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            lastStarted = timeProvider.GetTimestamp();
            started = true;

            // Hold the slot until the SDK call finishes, even if the caller gave up
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    readonly SemaphoreSlim semaphore = new(1);
    long lastStarted;
    bool started;
}