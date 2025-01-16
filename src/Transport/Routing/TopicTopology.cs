#nullable enable

namespace NServiceBus
{
    using System;

    /// <summary>
    /// Represents the topic topology used by <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    public abstract class TopicTopology
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        protected TopicTopology(TopologyOptions options) => Options = options;

        internal TopologyOptions Options { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static TopicTopology FromOptions(TopologyOptions options) =>
            options switch
            {
                MigrationTopologyOptions migrationOptions => new MigrationTopology(migrationOptions),
                TopicPerEventTopologyOptions perEventTopologyOptions => new TopicPerEventTopology(perEventTopologyOptions),
                _ => new TopicPerEventTopology(new TopicPerEventTopologyOptions
                {
                    PublishedEventToTopicsMap = options.PublishedEventToTopicsMap,
                    SubscribedEventToTopicsMap = options.SubscribedEventToTopicsMap,
                })
            };

        /// <summary>
        /// 
        /// </summary>
        public static TopicPerEventTopology Default => new(new TopicPerEventTopologyOptions());

        /// <summary>
        /// Returns the default bundle topology uses <c>bundle-1</c> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        public static MigrationTopology DefaultBundle => Single("bundle-1");

        /// <summary>
        /// Returns a topology using a single topic with the <paramref name="topicName"/> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        public static MigrationTopology Single(string topicName) => new(new MigrationTopologyOptions
        {
            TopicToPublishTo = topicName,
            TopicToSubscribeOn = topicName,
        });

        /// <summary>
        /// Returns a topology using a distinct name for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicToPublishTo">The topic name to publish to.</param>
        /// <param name="topicToSubscribeOn">The topic name to subscribe to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="topicToPublishTo"/> is equal to <paramref name="topicToSubscribeOn"/>.</exception>
        public static MigrationTopology Hierarchy(string topicToPublishTo, string topicToSubscribeOn)
        {
            var hierarchy = new MigrationTopology(new MigrationTopologyOptions
            {
                TopicToPublishTo = topicToPublishTo,
                TopicToSubscribeOn = topicToSubscribeOn,
            });
            if (!hierarchy.IsHierarchy)
            {
                throw new ArgumentException(
                    $"The '{nameof(topicToPublishTo)}' cannot be equal to '{nameof(topicToSubscribeOn)}'. Choose different names.");
            }

            return hierarchy;
        }
    }
}