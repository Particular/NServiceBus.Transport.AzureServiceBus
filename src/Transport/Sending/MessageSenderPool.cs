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
        public MessageSenderPool(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider, RetryPolicy retryPolicy)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
            this.retryPolicy = retryPolicy;

            senders = new ConcurrentDictionary<(string, (ServiceBusConnection, string)), ConcurrentQueue<MessageSender>>();
        }

        public MessageSender GetMessageSender(string destination, (ServiceBusConnection connection, string path) receiverConnectionAndPath)
        {
            var sendersForDestination = senders.GetOrAdd((destination, receiverConnectionAndPath), _ => new ConcurrentQueue<MessageSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosedOrClosing)
            {
                // Send-Via case
                if (receiverConnectionAndPath != (null, null))
                {
                    sender = new MessageSender(receiverConnectionAndPath.connection, destination, receiverConnectionAndPath.path, retryPolicy);
                }
                else
                {
                    if (tokenProvider == null)
                    {
                        sender = new MessageSender(connectionStringBuilder.GetNamespaceConnectionString(), destination, retryPolicy);
                    }
                    else
                    {
                        sender = new MessageSender(connectionStringBuilder.Endpoint, destination, tokenProvider, connectionStringBuilder.TransportType, retryPolicy);
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
            var path = sender.OwnsConnection ? sender.Path : sender.TransferDestinationPath;

            if (senders.TryGetValue((path, (connectionToUse, sender.ViaEntityPath)), out var sendersForDestination))
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
        readonly RetryPolicy retryPolicy;

        ConcurrentDictionary<(string destination, (ServiceBusConnection connnection, string incomingQueue)), ConcurrentQueue<MessageSender>> senders;
    }
}