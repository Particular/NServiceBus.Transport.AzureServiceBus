namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    static class Queue
    {
        public static Task Create(ManagementClient client, CommandArgument name, CommandOption<int> size, CommandOption partitioning)
        {
            var queueDescription = new QueueDescription(name.Value)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMB = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                EnablePartitioning = partitioning.HasValue()
            };

            return client.CreateQueueAsync(queueDescription);
        }

        public static Task Delete(ManagementClient client, CommandArgument name)
        {
            return client.DeleteQueueAsync(name.Value);
        }
    }
}