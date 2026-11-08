namespace NServiceBus.Transport.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

sealed class DestinationSender(string destination, ServiceBusSender sender, TopicCreator? topicCreator)
{
    public ServiceBusSender Sender => sender;

    public ValueTask EnsureTopicExists(CancellationToken cancellationToken = default) =>
        topicCreator is null || Volatile.Read(ref topicExists) ? default : EnsureTopicExists(topicCreator, cancellationToken);

    async ValueTask EnsureTopicExists(TopicCreator creator, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (topicExists)
            {
                return;
            }

            await creator.CreateIfMissing(destination, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref topicExists, true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task Close(CancellationToken cancellationToken = default)
    {
        await using (sender.ConfigureAwait(false))
        {
            await sender.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    readonly SemaphoreSlim gate = new(1, 1);
    // Never reset, so topics deleted while the endpoint runs aren't recreated
    bool topicExists;
}
