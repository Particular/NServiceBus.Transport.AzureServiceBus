namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;

    class MessageSenderPool
    {
        public MessageSenderPool(string connectionString, ServiceBusClientOptions serviceBusClientOptions, TokenCredential tokenCredential)
        {
            this.connectionString = connectionString;
            this.serviceBusClientOptions = serviceBusClientOptions;
            this.tokenCredential = tokenCredential;

            senders = new ConcurrentDictionary<(string, (string, string)), ConcurrentQueue<ServiceBusSender>>();
        }

        public ServiceBusSender GetMessageSender(string destination, (string connection, string path) receiverConnectionAndPath)
        {
            var sendersForDestination = senders.GetOrAdd((destination, receiverConnectionAndPath), _ => new ConcurrentQueue<ServiceBusSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosed)
            {
                // Send-Via case
                // TODO: should set the ServiceBusClientOptions { EnableCrossEntityTransactions = true }
                if (receiverConnectionAndPath != (null, null))
                {
                    var client = new ServiceBusClient(connectionString, tokenCredential, serviceBusClientOptions);

                    sender = client.CreateSender(destination);
                }
                else
                {
                    if (tokenCredential == null)
                    {
                        //sender = new MessageSender(connectionStringBuilder.GetNamespaceConnectionString(), destination, retryPolicy);
                        var client = new ServiceBusClient(connectionString, serviceBusClientOptions);

                        sender = client.CreateSender(destination);
                    }
                    else
                    {
                        var client = new ServiceBusClient(connectionString, tokenCredential, serviceBusClientOptions);

                        sender = client.CreateSender(destination);
                        //sender = new MessageSender(connectionStringBuilder.Endpoint, destination, tokenProvider, connectionStringBuilder.TransportType, retryPolicy);
                    }
                }
            }

            return sender;
        }

        public void ReturnMessageSender(ServiceBusSender sender)
        {
            if (sender.IsClosed)
            {
                return;
            }

            // var connectionToUse = sender.FullyQualifiedNamespace.OwnsConnection ? null : sender.ServiceBusConnection;
            // var path = sender.OwnsConnection ? sender.Path : sender.TransferDestinationPath;
            //
            // if (senders.TryGetValue((path, (connectionToUse, sender.ViaEntityPath)), out var sendersForDestination))
            // {
            //     sendersForDestination.Enqueue(sender);
            // }
            //TODO: this tuple is wrong, what should be in destination vs ViaEntityPath
            if (senders.TryGetValue((sender.EntityPath, (sender.FullyQualifiedNamespace, sender.EntityPath)), out var sendersForDestination))
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

        readonly ServiceBusClientOptions serviceBusClientOptions;
        readonly string connectionString;
        readonly TokenCredential tokenCredential;

        ConcurrentDictionary<(string destination, (string connnection, string incomingQueue)), ConcurrentQueue<ServiceBusSender>> senders;
    }
}