namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Logging;
using Receiving;

sealed class SubscriptionForwarders(Func<ServiceBusClient> clientFactory, string inputQueue, TimeSpan timeBeforeTriggeringCircuitBreaker, Action<string, Exception, CancellationToken> criticalErrorAction) : ISubscriptionForwarders
{
    readonly Dictionary<(string Topic, string Subscription), OrderedSubscriptionForwarder> forwarders = [];
    readonly SemaphoreSlim lockSemaphore = new(1, 1);
    bool started;

    // Until Start is called forwarders are only recorded, because whether SubscribeAll ran is only known when receiving starts
    public async Task Register(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default)
    {
        await lockSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Synchronized via lockSemaphore
            // ReSharper disable InconsistentlySynchronizedField
            if (!forwarders.TryGetValue((topic, subscription), out var forwarder))
            {
                forwarder = new OrderedSubscriptionForwarder(clientFactory, topic, subscription, inputQueue, timeBeforeTriggeringCircuitBreaker, criticalErrorAction);
                forwarders[(topic, subscription)] = forwarder;
            }

            forwarder.RegisterType(eventTypeFullName);

            if (started)
            {
                await forwarder.Start(cancellationToken).ConfigureAwait(false);
            }
            // ReSharper restore InconsistentlySynchronizedField
        }
        finally
        {
            lockSemaphore.Release();
        }
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        await lockSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            started = true;

            var startedForwarders = new List<OrderedSubscriptionForwarder>(forwarders.Count);
            var allStarted = false;
            try
            {
                foreach (var forwarder in forwarders.Values)
                {
                    await forwarder.Start(cancellationToken).ConfigureAwait(false);
                    startedForwarders.Add(forwarder);
                }

                allStarted = true;
            }
            finally
            {
                if (!allStarted)
                {
                    // Roll back so that a failed start leaves nothing running and starting again starts all forwarders.
                    // The failing forwarder already rolled back its own start, and failures to stop are logged by the forwarder.
                    started = false;

                    foreach (var forwarder in startedForwarders)
                    {
                        // Not canceled, so that the rollback also completes when the start was canceled
                        await forwarder.Stop(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            lockSemaphore.Release();
        }
    }

    public async Task Unregister(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default)
    {
        await lockSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Synchronized via lockSemaphore
            // ReSharper disable InconsistentlySynchronizedField
            if (forwarders.TryGetValue((topic, subscription), out var forwarder)
                && forwarder.UnregisterType(eventTypeFullName))
            {
                forwarders.Remove((topic, subscription));

                try
                {
                    await forwarder.Stop(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await forwarder.DisposeAsync().ConfigureAwait(false);
                }
            }
            // ReSharper restore InconsistentlySynchronizedField
        }
        finally
        {
            lockSemaphore.Release();
        }
    }

    public async Task Shutdown(CancellationToken cancellationToken = default)
    {
        //HINT: No need to lock because we are shutting down the endpoint and noone can subscribe or unsubscribe now
        foreach (var forwarder in forwarders.Values)
        {
            try
            {
                await forwarder.Stop(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Operation canceled while stopping a subscription forwarder.", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to stop a subscription forwarder.", ex);
            }
            finally
            {
                await forwarder.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    static readonly ILog Logger = LogManager.GetLogger<SubscriptionForwarders>();
}