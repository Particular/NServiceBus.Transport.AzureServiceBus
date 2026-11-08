namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;

sealed class TopicCreator(ServiceBusAdministrationClient administrationClient, bool enablePartitioning, bool supportOrdering, int maxSizeInMegabytes)
{
    public async Task CreateIfMissing(string topic, CancellationToken cancellationToken = default)
    {
        // A failed check is an error, not "missing"
        var exists = await administrationClient.TopicExistsAsync(topic, cancellationToken).ConfigureAwait(false);
        if (exists.Value)
        {
            return;
        }

        try
        {
            await administrationClient.CreateTopicAsync(Options(topic, enablePartitioning, supportOrdering, maxSizeInMegabytes), cancellationToken).ConfigureAwait(false);

            if (Log.IsInfoEnabled)
            {
                Log.Info($"Created topic {topic} before publishing to it. Events published before a subscription exists are not delivered.");
            }
        }
        catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Created concurrently elsewhere
        }
        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
        {
            throw new InvalidOperationException($"Creating topic {topic} before publishing failed.", ex);
        }
    }

    // Publishers and subscribers both create topics, and endpoints sharing a topic must agree on its settings
    public static CreateTopicOptions Options(string topic, bool enablePartitioning, bool supportOrdering, int maxSizeInMegabytes) =>
        new(topic)
        {
            EnableBatchedOperations = true,
            EnablePartitioning = enablePartitioning,
            SupportOrdering = supportOrdering,
            MaxSizeInMegabytes = maxSizeInMegabytes
        };

    static readonly ILog Log = LogManager.GetLogger<TopicCreator>();
}
