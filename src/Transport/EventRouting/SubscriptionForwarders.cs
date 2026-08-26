namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Receiving;

sealed class SubscriptionForwarders(Func<ServiceBusClient> clientFactory, string inputQueue) : ISubscriptionForwarders
{
    readonly Dictionary<(string Topic, string Subscription), OrderedSubscriptionForwarder> forwarders = [];
    readonly SemaphoreSlim lockSemaphore = new(1, 1);

    public async Task StartForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default)
    {
        await lockSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Synchronized via lockSemaphore
            // ReSharper disable InconsistentlySynchronizedField
            if (!forwarders.TryGetValue((topic, subscription), out var forwarder))
            {
                forwarder = new OrderedSubscriptionForwarder(clientFactory(), topic, subscription, inputQueue);
                await forwarder.Start(cancellationToken).ConfigureAwait(false);
                forwarders[(topic, subscription)] = forwarder;

            }
            // ReSharper restore InconsistentlySynchronizedField

            forwarder.RegisterType(eventTypeFullName);
        }
        finally
        {
            lockSemaphore.Release();
        }
    }

    public async Task StopForwarding(string topic, string subscription, string eventTypeFullName, CancellationToken cancellationToken = default)
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
                await forwarder.Stop(cancellationToken).ConfigureAwait(false);
            }
            // ReSharper restore InconsistentlySynchronizedField
        }
        finally
        {
            lockSemaphore.Release();
        }
    }
}