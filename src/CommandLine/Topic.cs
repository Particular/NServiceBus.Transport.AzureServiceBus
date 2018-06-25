namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    static class Topic
    {
        public static Task Create(ManagementClient client, CommandOption topicName, CommandOption<int> size, CommandOption partitioning)
        {
            var topicNameToUse = topicName.HasValue() ? topicName.Value() : "bundle-1";

            var topicDescription = new TopicDescription(topicNameToUse)
            {
                EnableBatchedOperations = true,
                EnablePartitioning = partitioning.HasValue(),
                MaxSizeInMB = (size.HasValue() ? size.ParsedValue : 5) * 1024,
            };

            return client.CreateTopicAsync(topicDescription);
        }
    }
}