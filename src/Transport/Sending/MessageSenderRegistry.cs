namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

sealed class MessageSenderRegistry
{
    public ServiceBusSender GetMessageSender(string destination, ServiceBusClient client)
    {
        // According to the client SDK guidelines we can safely use these client objects for concurrent asynchronous
        // operations and from multiple threads.
        // see https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements
        var lazySender = destinationToSenderMapping.GetOrAdd((destination, client),
            static arg =>
            {
                (string innerDestination, ServiceBusClient innerClient) = arg;
                // Unfortunately Lazy closure allocates but this should be fine since the majority of the
                // execution path will fall into the get and not the add.
                return new Lazy<ServiceBusSender>(() => innerClient.CreateSender(innerDestination, new ServiceBusSenderOptions { Identifier = $"Sender-{innerDestination}-{Guid.NewGuid()}" }), LazyThreadSafetyMode.ExecutionAndPublication);
            });
        return lazySender.Value;
    }

    public Task Close(CancellationToken cancellationToken = default)
    {
        static async Task CloseAndDispose(ServiceBusSender sender, CancellationToken cancellationToken)
        {
            await using (sender.ConfigureAwait(false))
            {
                await sender.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        var tasks = new List<Task>(destinationToSenderMapping.Keys.Count);
        foreach (var key in destinationToSenderMapping.Keys)
        {
            var queue = destinationToSenderMapping[key];

            if (!queue.IsValueCreated)
            {
                continue;
            }


            tasks.Add(CloseAndDispose(queue.Value, cancellationToken));
        }
        return Task.WhenAll(tasks);
    }

    readonly ConcurrentDictionary<(string destination, ServiceBusClient client), Lazy<ServiceBusSender>> destinationToSenderMapping = new();
}