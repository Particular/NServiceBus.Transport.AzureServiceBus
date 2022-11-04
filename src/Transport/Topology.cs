namespace NServiceBus
{
    using System;
    using Transport.AzureServiceBus;

    /// <summary>
    ///
    /// </summary>
    public readonly struct Topology
    {
        /// <summary>
        ///
        /// </summary>
        public string TopicToPublishTo { get; }
        /// <summary>
        ///
        /// </summary>
        public string TopicToSubscribeOn { get; }

        /// <summary>
        ///
        /// </summary>
        public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

        Topology(string topicToPublishTo, string topicToSubscribeOn)
        {
            Guard.AgainstNullAndEmpty(nameof(topicToPublishTo), topicToPublishTo);
            Guard.AgainstNullAndEmpty(nameof(topicToSubscribeOn), topicToSubscribeOn);

            TopicToPublishTo = topicToPublishTo;
            TopicToSubscribeOn = topicToSubscribeOn;
        }

        /// <summary>
        ///
        /// </summary>
        public static Topology DefaultBundle => Single("bundle-1");
        /// <summary>
        ///
        /// </summary>
        /// <param name="topicName"></param>
        /// <returns></returns>
        public static Topology Single(string topicName) => new(topicName, topicName);

        /// <summary>
        ///
        /// </summary>
        /// <param name="topicToPublishTo"></param>
        /// <param name="topicToSubscribeOn"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Topology Hierarchy(string topicToPublishTo, string topicToSubscribeOn)
        {
            var hierarchy = new Topology(topicToPublishTo, topicToSubscribeOn);
            if (!hierarchy.IsHierarchy)
            {
                throw new ArgumentException("TBD");
            }
            return hierarchy;
        }
    }
}