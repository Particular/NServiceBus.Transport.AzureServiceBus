namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;

class QueueCreator(AzureServiceBusTransport transportSettings)
{
    static readonly ILog Logger = LogManager.GetLogger<QueueCreator>();

    public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues,
        CancellationToken cancellationToken = default)
    {
        foreach (var address in queues)
        {
            var queue = new CreateQueueOptions(address)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes,
                EnablePartitioning = transportSettings.EnablePartitioning
            };

            try
            {
                await adminClient.CreateQueueAsync(queue, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Logger.Debug($"Queue {queue.Name} already exists");
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Queue creation for {queue.Name} is already in progress");
            }
        }
    }
}