namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Serializable object that defines the topic-per-event topology
/// </summary>
public class TopicPerEventTopologyOptions : TopologyOptions
{
    /// <summary>
    /// Determines if an exception should be thrown when attempting to publish an event not mapped in PublishedEventToTopicsMap
    /// </summary>
    public bool ThrowIfUnmappedEventTypes { get; set; } = false;

    internal static TopicPerEventTopologyOptions From(TopologyOptions options) =>
        new()
        {
            PublishedEventToTopicsMap = options.PublishedEventToTopicsMap,
            QueueNameToSubscriptionNameMap = options.QueueNameToSubscriptionNameMap,
            SubscribedEventToTopicsMap = options.SubscribedEventToTopicsMap
        };
}
