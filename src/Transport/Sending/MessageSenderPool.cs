namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Core;

    class MessageSenderPool
    {
        public MessageSenderPool(string connectionString)
        {
            this.connectionString = connectionString;
            senders = new ConcurrentDictionary<string, ConcurrentQueue<MessageSender>>();
        }

        public MessageSender GetMessageSender(string entityPath)
        {
            throw new NotImplementedException();
        }

        public void ReturnMessageSender(MessageSender sender)
        {
            throw new NotImplementedException();
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

        ConcurrentDictionary<string, ConcurrentQueue<MessageSender>> senders;
    }
}