namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    class MessageSenderPool
    {
        public MessageSenderPool(ServiceBusClient client)
        {
            defaultClient = client;

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

        public Task Close(CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();

            foreach (var key in senders.Keys)
            {
                var queue = senders[key];

                foreach (var sender in queue)
                {
                    tasks.Add(sender.CloseAsync(cancellationToken));
                }
            }

            return Task.WhenAll(tasks);
        }

        readonly ServiceBusClient defaultClient;
        ConcurrentDictionary<(string destination, ServiceBusClient client), ConcurrentQueue<ServiceBusSender>> senders;
    }
}