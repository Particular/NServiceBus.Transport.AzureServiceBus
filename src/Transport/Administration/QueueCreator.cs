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

    public async Task Create(ServiceBusAdministrationClient adminClient, string[] queues, string? instanceName = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var address in queues)
        {
            var queue = new CreateQueueOptions(address)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = transportSettings.MaxDeliveryCount,
                MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes,
                EnablePartitioning = transportSettings.EnablePartitioning
            };

            // Only apply AutoDeleteOnIdle if an instance name is provided to avoid unintentional deletions
            // of shared queues, e.g. error.
            if (instanceName is not null
               && string.Equals(instanceName, address, StringComparison.Ordinal)
               && transportSettings.AutoDeleteOnIdle is not null)
            {
                queue.AutoDeleteOnIdle = transportSettings.AutoDeleteOnIdle.Value;
            }

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
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Queue creation for {queue.Name} is already in progress");
            }
        }
    }
}