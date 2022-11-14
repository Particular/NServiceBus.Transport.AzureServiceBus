namespace NServiceBus
{
    using System;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Represents the topic topology used by <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    public readonly struct TopicTopology : IEquatable<TopicTopology>
    {
        /// <summary>
        /// Gets the topic name of the topic where all events are published to.
        /// </summary>
        public string TopicToPublishTo { get; }

        /// <summary>
        /// Gets the topic name of the topic where all subscriptions are managed on.
        /// </summary>
        public string TopicToSubscribeOn { get; }

        /// <summary>
        /// Gets whether the current topic topology represents a hierarchy.
        /// </summary>
        public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

        TopicTopology(string topicToPublishTo, string topicToSubscribeOn)
        {
            Guard.AgainstNullAndEmpty(nameof(topicToPublishTo), topicToPublishTo);
            Guard.AgainstNullAndEmpty(nameof(topicToSubscribeOn), topicToSubscribeOn);

            TopicToPublishTo = topicToPublishTo;
            TopicToSubscribeOn = topicToSubscribeOn;
        }

        /// <summary>
        /// Returns the default bundle topology uses <c>bundle-1</c> for <see cref="TopicToPublishTo"/> and <see cref="TopicToSubscribeOn"/>
        /// </summary>
        public static TopicTopology DefaultBundle => Single("bundle-1");

        /// <summary>
        /// Returns a topology using a single topic with the <paramref name="topicName"/> for <see cref="TopicToPublishTo"/> and <see cref="TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        public static TopicTopology Single(string topicName) => new(topicName, topicName);

        /// <summary>
        /// Returns a topology using a distinct name for <see cref="TopicToPublishTo"/> and <see cref="TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicToPublishTo">The topic name to publish to.</param>
        /// <param name="topicToSubscribeOn">The topic name to subscribe to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="topicToPublishTo"/> is equal to <paramref name="topicToSubscribeOn"/>.</exception>
        public static TopicTopology Hierarchy(string topicToPublishTo, string topicToSubscribeOn)
        {
            var hierarchy = new TopicTopology(topicToPublishTo, topicToSubscribeOn);
            if (!hierarchy.IsHierarchy)
            {
                throw new ArgumentException($"The '{nameof(topicToPublishTo)}' cannot be equal to '{nameof(topicToSubscribeOn)}'. Choose different names.");
            }
            return hierarchy;
        }

        /// <inheritdoc />
        public bool Equals(TopicTopology other) => string.Equals(TopicToPublishTo, other.TopicToPublishTo, StringComparison.OrdinalIgnoreCase) && string.Equals(TopicToSubscribeOn, other.TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TopicTopology other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(TopicToPublishTo) * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(TopicToSubscribeOn);
            }
        }

#pragma warning disable CS1591
        public static bool operator ==(TopicTopology left, TopicTopology right) => left.Equals(right);

        public static bool operator !=(TopicTopology left, TopicTopology right) => !left.Equals(right);
#pragma warning restore CS1591
    }
}