namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Queue
    {
        public static async Task Create(ServiceBusAdministrationClient client, CommandArgument name, CommandOption<int> size, CommandOption partitioning, CommandOption hierarchyNamespace, CommandOption forwardDeadLetteredMessagesTo)
        {
            var queueDescription = BuildDefaultCreateQueueOptions(name.ToHierarchyNamespaceAwareDestination(hierarchyNamespace));

            if (forwardDeadLetteredMessagesTo.HasValue())
            {
                queueDescription.ForwardDeadLetteredMessagesTo = forwardDeadLetteredMessagesTo.Value().ToHierarchyNamespaceAwareDestination(hierarchyNamespace);
                var deadLetterTargetQueueOptions = BuildDefaultCreateQueueOptions(queueDescription.ForwardDeadLetteredMessagesTo);

                try
                {
                    await client.CreateQueueAsync(deadLetterTargetQueueOptions);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Queue to forward DLQ messages to, '{queueDescription.Name}', already exists, skipping creation");
                }
            }

            await client.CreateQueueAsync(queueDescription);

            CreateQueueOptions BuildDefaultCreateQueueOptions(string queueName) => new(queueName)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMegabytes = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                EnablePartitioning = partitioning.HasValue()
            };
        }

        public static Task Delete(ServiceBusAdministrationClient client, CommandArgument name, CommandOption hierarchyNamespace)
            => client.DeleteQueueAsync(name.ToHierarchyNamespaceAwareDestination(hierarchyNamespace));
    }
}