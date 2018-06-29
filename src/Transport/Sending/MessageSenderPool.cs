namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Azure.ServiceBus.Primitives;

    class MessageSenderPool
    {
        public MessageSenderPool(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;

            senders = new ConcurrentDictionary<(string, ServiceBusConnection, string), ConcurrentQueue<MessageSender>>();
        }

        public MessageSender GetMessageSender(string destination, ServiceBusConnection connection, string incomingQueue)
        {
            var sendersForDestination = senders.GetOrAdd((destination, connection, incomingQueue), tuple => new ConcurrentQueue<MessageSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosedOrClosing)
            {
                // Send-Via case
                if (connection != null)
                {
                    sender = new MessageSender(connection, destination, incomingQueue);
                }
                else
                {
                    if (tokenProvider == null)
                    {
                        sender = new MessageSender(connectionStringBuilder.GetNamespaceConnectionString(), destination);
                    }
                    else
                    {
                        sender = new MessageSender(connectionStringBuilder.Endpoint, destination, tokenProvider, connectionStringBuilder.TransportType);
                    }
                }
            }

            return sender;
        }

        public void ReturnMessageSender(MessageSender sender)
        {
            if (sender.IsClosedOrClosing)
            {
                return;
            }

            var connectionToUse = sender.OwnsConnection ? null : sender.ServiceBusConnection;

            if (senders.TryGetValue((sender.Path, connectionToUse, sender.TransferDestinationPath), out var sendersForDestination))
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

        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly ITokenProvider tokenProvider;

        ConcurrentDictionary<(string destination, ServiceBusConnection connnection, string incomingQueue), ConcurrentQueue<MessageSender>> senders;
    }
}