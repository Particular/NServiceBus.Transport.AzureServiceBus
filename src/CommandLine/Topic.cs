﻿namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Topic
    {
        public static Task Create(ServiceBusAdministrationClient client, CommandOption topicNameToUse, CommandOption<int> size,
            CommandOption partitioning) =>
            Create(client, topicNameToUse.Value(), size, partitioning);

        public static Task Create(ServiceBusAdministrationClient client, CommandArgument topicNameToUse, CommandOption<int> size,
            CommandOption partitioning) =>
            Create(client, topicNameToUse.Value, size, partitioning);

        static Task Create(ServiceBusAdministrationClient client, string topicNameToUse, CommandOption<int> size, CommandOption partitioning)
        {
            var options = new CreateTopicOptions(topicNameToUse)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = partitioning.HasValue(),
                MaxSizeInMegabytes = (size.HasValue() ? size.ParsedValue : 5) * 1024
            };

            return client.CreateTopicAsync(options);
        }

        public const string DefaultTopicName = "bundle-1";
    }
}