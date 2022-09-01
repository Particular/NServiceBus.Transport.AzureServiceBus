namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;

    class MessageSenderPool
    {
        public MessageSenderPool(string connectionString, TokenCredential tokenCredential, ServiceBusRetryOptions retryOptions, ServiceBusTransportType transportType, TransportTransactionMode transactionMode)
        {
            var serviceBusClientOptions = new ServiceBusClientOptions()
            {
                TransportType = transportType,
                EnableCrossEntityTransactions = transactionMode == TransportTransactionMode.SendsAtomicWithReceive
            };

            if (retryOptions != null)
            {
                serviceBusClientOptions.RetryOptions = retryOptions;
            }

            var fullyQualifiedNamespace = ServiceBusConnectionStringProperties.Parse(connectionString).FullyQualifiedNamespace;

            defaultClient = tokenCredential != null
                    ? new ServiceBusClient(fullyQualifiedNamespace, tokenCredential, serviceBusClientOptions)
                    : new ServiceBusClient(connectionString, serviceBusClientOptions);

            senders = new ConcurrentDictionary<(string, ServiceBusClient), ConcurrentQueue<ServiceBusSender>>();
        }

        public ServiceBusSender GetMessageSender(string destination, ServiceBusClient client)
        {
            var sendersForDestination = senders.GetOrAdd((destination, client ?? defaultClient), _ => new ConcurrentQueue<ServiceBusSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosed)
            {
                sender = (client ?? defaultClient).CreateSender(destination);
            }

            return sender;
        }

        public void ReturnMessageSender(ServiceBusSender sender, ServiceBusClient client)
        {
            if (sender.IsClosed)
            {
                return;
            }

            if (senders.TryGetValue((sender.EntityPath, client ?? defaultClient), out var sendersForDestination))
            {
                sendersForDestination.Enqueue(sender);
            }
        }

        public Task Close()
        {
            var tasks = new List<Task>();

            foreach (var key in senders.Keys)
            {
                var queue = senders[key];

                foreach (var sender in queue)
                {
                    tasks.Add(sender.CloseAsync());
                }
            }

            return Task.WhenAll(tasks);
        }

        readonly ServiceBusClient defaultClient;
        ConcurrentDictionary<(string destination, ServiceBusClient client), ConcurrentQueue<ServiceBusSender>> senders;
    }
}