namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;

    class MessageSenderPool
    {
        public MessageSenderPool(string connectionString)
        {
            this.connectionString = connectionString;
            senders = new ConcurrentDictionary<(string, ServiceBusConnection, string), ConcurrentQueue<MessageSender>>();
        }

        public MessageSender GetMessageSender(string destination, ServiceBusConnection connection = null, string incomingQueue = null)
        {
            var sendersForDestination = senders.GetOrAdd((destination, connection, incomingQueue), tuple => new ConcurrentQueue<MessageSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosedOrClosing)
            {
                if (connection != null)
                {
                    sender = new MessageSender(connection, destination, incomingQueue);
                }
                else
                {
                    sender = new MessageSender(connectionString, destination);
                }
            }

            return sender;
        }

        public void ReturnMessageSender(MessageSender sender, ServiceBusConnection connection = null)
        {
            if (sender.IsClosedOrClosing)
            {
                return;
            }

            if (senders.TryGetValue((sender.Path, connection, sender.TransferDestinationPath), out var sendersForDestination))
            {
                sendersForDestination.Enqueue(sender);
            }
        }

        public async Task Close()
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

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        readonly string connectionString;

        ConcurrentDictionary<(string destination, ServiceBusConnection connnection, string incomingQueue), ConcurrentQueue<MessageSender>> senders;
    }
}