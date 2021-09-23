namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Topic
    {
        public static Task Create(ServiceBusAdministrationClient client, CommandOption topicName, CommandOption<int> size, CommandOption partitioning)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : DefaultTopicName;

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