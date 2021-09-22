namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    class MessageSenderPool
    {
        public MessageSenderPool(ServiceBusClient serviceBusClient)
        {
            this.serviceBusClient = serviceBusClient;

            senders = new ConcurrentDictionary<string, ConcurrentQueue<ServiceBusSender>>();
        }

        public ServiceBusSender GetMessageSender(string destination)
        {
            var sendersForDestination = senders.GetOrAdd(destination, _ => new ConcurrentQueue<ServiceBusSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosed)
            {
                sender = serviceBusClient.CreateSender(destination);
            }

            return sender;
        }

        public void ReturnMessageSender(ServiceBusSender sender)
        {
            if (sender.IsClosed)
            {
                return;
            }

            if (senders.TryGetValue(sender.EntityPath, out var sendersForDestination))
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

        readonly ServiceBusClient serviceBusClient;

        ConcurrentDictionary<string, ConcurrentQueue<ServiceBusSender>> senders;
    }
}