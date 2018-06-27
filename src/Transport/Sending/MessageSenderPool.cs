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

        public MessageSender GetMessageSender(string destination, ServiceBusConnection connection, string incomingQueue)
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

        // TODO: connection argument won't be required when https://github.com/Azure/azure-service-bus-dotnet/issues/482 is fixed
        public void ReturnMessageSender(MessageSender sender, ServiceBusConnection connection)
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