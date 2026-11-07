namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;

// Shared by publisher-side topic checks and creates in one endpoint. This limits logical SDK calls, not each HTTP retry.
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

            // Keep the slot until the SDK call actually finishes, even if its caller has stopped waiting for it.
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