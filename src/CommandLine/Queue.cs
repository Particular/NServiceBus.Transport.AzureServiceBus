namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Queue
    {
        public static Task Create(ServiceBusAdministrationClient client, CommandArgument name, CommandOption<int> size, CommandOption partitioning)
        {
            var queueDescription = new CreateQueueOptions(name.Value)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMegabytes = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                EnablePartitioning = partitioning.HasValue()
            };

            return client.CreateQueueAsync(queueDescription);
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument name) => client.DeleteQueueAsync(name.Value);
    }
}