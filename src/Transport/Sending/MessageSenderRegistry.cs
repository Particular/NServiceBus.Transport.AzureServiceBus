namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;

    sealed class MessageSenderRegistry
    {
        public MessageSenderRegistry(string connectionString, TokenCredential tokenCredential, ServiceBusRetryOptions retryOptions, ServiceBusTransportType transportType, TransportTransactionMode transactionMode)
        {
            var serviceBusClientOptions = new ServiceBusClientOptions
            {
                TransportType = transportType,
                // for the default client we never want things to automatically use cross entity transaction
                EnableCrossEntityTransactions = false,
            };

            if (retryOptions != null)
            {
                serviceBusClientOptions.RetryOptions = retryOptions;
            }

            defaultClient = tokenCredential != null
                ? new ServiceBusClient(connectionString, tokenCredential, serviceBusClientOptions)
                : new ServiceBusClient(connectionString, serviceBusClientOptions);

            destinationToSenderMapping = new ConcurrentDictionary<(string, ServiceBusClient), Lazy<ServiceBusSender>>();
        }

        public ServiceBusSender GetMessageSender(string destination, ServiceBusClient client)
        {
            // According to the client SDK guidelines we can safely use these client objects for concurrent asynchronous
            // operations and from multiple threads.
            // see https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements
            var lazySender = destinationToSenderMapping.GetOrAdd((destination, client ?? defaultClient),
                arg =>
                {
                    (string innerDestination, ServiceBusClient innerClient) = arg;
                    // Unfortunately Lazy closure allocates but this should be fine since the majority of the
                    // execution path will fall into the get and not the add.
                    return new Lazy<ServiceBusSender>(() => innerClient.CreateSender(innerDestination), LazyThreadSafetyMode.ExecutionAndPublication);
                });
            return lazySender.Value;
        }

        public Task Close(CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>(destinationToSenderMapping.Keys.Count + 1);
            foreach (var key in destinationToSenderMapping.Keys)
            {
                var queue = destinationToSenderMapping[key];

                if (queue.IsValueCreated)
                {
                    tasks.Add(queue.Value.CloseAsync(cancellationToken));
                }
            }
            tasks.Add(defaultClient.DisposeAsync().AsTask());
            return Task.WhenAll(tasks);
        }

        readonly ServiceBusClient defaultClient;
        readonly ConcurrentDictionary<(string destination, ServiceBusClient client), Lazy<ServiceBusSender>> destinationToSenderMapping;
    }
}