namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;

class QueueCreator
{
    static readonly ILog Logger = LogManager.GetLogger<QueueCreator>();

    public async Task Create(ServiceBusAdministrationClient adminClient, IReadOnlyCollection<CreateQueueOptions> queues,
        CancellationToken cancellationToken = default)
    {
        foreach (var queue in queues)
        {
            try
            {
                await adminClient.CreateQueueAsync(queue, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Queue {queue.Name} already exists");
                }
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient) // An operation is in progress.
            {
                Logger.Info($"Queue creation for {queue.Name} is already in progress");
            }
        }
    }
}