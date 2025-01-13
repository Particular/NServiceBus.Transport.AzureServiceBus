#nullable enable

namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Represents the topic topology used by <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    public class TopicTopology
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        protected TopicTopology(TopologyOptions? options = null) => Options = options ?? new TopologyOptions();

        internal TopologyOptions Options { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static MigrationTopology FromOptions(TopologyOptions options) => new(options);

        /// <summary>
        /// 
        /// </summary>
        public static TopicPerEventTopology Default => new();

        /// <summary>
        /// Returns the default bundle topology uses <c>bundle-1</c> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        public static MigrationTopology DefaultBundle => Single("bundle-1");

        /// <summary>
        /// Returns a topology using a single topic with the <paramref name="topicName"/> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        public static MigrationTopology Single(string topicName) => new(topicName, topicName);

        /// <summary>
        /// Returns a topology using a distinct name for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicToPublishTo">The topic name to publish to.</param>
        /// <param name="topicToSubscribeOn">The topic name to subscribe to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="topicToPublishTo"/> is equal to <paramref name="topicToSubscribeOn"/>.</exception>
        public static MigrationTopology Hierarchy(string topicToPublishTo, string topicToSubscribeOn)
        {
            var hierarchy = new MigrationTopology(topicToPublishTo, topicToSubscribeOn);
            if (!hierarchy.IsHierarchy)
            {
                throw new ArgumentException($"The '{nameof(topicToPublishTo)}' cannot be equal to '{nameof(topicToSubscribeOn)}'. Choose different names.");
            }
            return hierarchy;
        }
    }

    /// <summary>
    /// TODO we probably need some kind of validation method that checks against invalid configurations?
    /// </summary>
    public class TopologyOptions
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<string, string> PublishedEventToTopicsMap { get; } = [];

        /// <summary>
        /// 
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; } = [];

        /// <summary>
        /// 
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<string, (string TopicToPublishTo, string TopicToSubscribeOn)> EventsToTopicMigrationMap { get; } = [];

        internal string GetPublishDestination(Type eventType)
        {
            var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            return publishedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
                @this.EventsToTopicMigrationMap.TryGetValue(fullName,
                    out var extractDestination)
                    ? extractDestination.TopicToPublishTo
                    : @this.PublishedEventToTopicsMap.GetValueOrDefault(fullName, fullName), this);
        }

        internal (string Topic, bool RequiresRule)[] GetSubscribeDestinations(Type eventType)
        {
            var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            return subscribedEventToTopicsCache.GetOrAdd(eventTypeFullName, static (fullName, @this) =>
                @this.EventsToTopicMigrationMap.TryGetValue(fullName,
                out var extractDestination)
                ? [(extractDestination.TopicToSubscribeOn, true)]
                : @this.SubscribedEventToTopicsMap.GetValueOrDefault(fullName, [fullName]).Select(x => (x, false)).ToArray(), this);
        }

        readonly ConcurrentDictionary<string, string> publishedEventToTopicsCache = new();
        readonly ConcurrentDictionary<string, (string Topic, bool RequiresRule)[]> subscribedEventToTopicsCache = new();
    }

    public class TopicPerEventTopology : TopicTopology
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicName"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishTo<TEventType>(string topicName)
        {
            // TODO Last one wins?
            Options.PublishedEventToTopicsMap[typeof(TEventType).FullName ?? throw new InvalidOperationException()] = topicName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="topicName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishTo(Type type, string topicName)
        {
            // TODO Last one wins?
            Options.PublishedEventToTopicsMap[type.FullName ?? throw new InvalidOperationException()] = topicName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicName"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeTo<TEventType>(string topicName) => SubscribeTo(typeof(TEventType), topicName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="topicName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeTo(Type type, string topicName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(type.FullName);

            var eventTypeFullName = type.FullName;
            if (Options.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
            {
                topics.Add(topicName);
            }
            else
            {
                Options.SubscribedEventToTopicsMap[eventTypeFullName] = [topicName];
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class MigrationTopology : TopicTopology
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

        internal MigrationTopology(string topicToPublishTo, string topicToSubscribeOn)
        {
            Guard.AgainstNullAndEmpty(nameof(topicToPublishTo), topicToPublishTo);
            Guard.AgainstNullAndEmpty(nameof(topicToSubscribeOn), topicToSubscribeOn);

            TopicToPublishTo = topicToPublishTo;
            TopicToSubscribeOn = topicToSubscribeOn;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        public MigrationTopology(TopologyOptions options) : base(options)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicName"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishTo<TEventType>(string topicName)
        {
            // TODO Last one wins?
            Options.PublishedEventToTopicsMap[typeof(TEventType).FullName ?? throw new InvalidOperationException()] = topicName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="topicName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishTo(Type type, string topicName)
        {
            // TODO Last one wins?
            Options.PublishedEventToTopicsMap[type.FullName ?? throw new InvalidOperationException()] = topicName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicName"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeTo<TEventType>(string topicName) => SubscribeTo(typeof(TEventType), topicName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="topicName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeTo(Type type, string topicName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(type.FullName);

            var eventTypeFullName = type.FullName;
            if (Options.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var topics))
            {
                topics.Add(topicName);
            }
            else
            {
                Options.SubscribedEventToTopicsMap[eventTypeFullName] = [topicName];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishToDefaultTopic<TEventType>()
        {
            _ = Options.EventsToTopicMigrationMap.TryAdd(typeof(TEventType).FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void PublishToDefaultTopic(Type type)
        {
            _ = Options.EventsToTopicMigrationMap.TryAdd(type.FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEventType"></typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeToDefaultTopic<TEventType>()
        {
            _ = Options.EventsToTopicMigrationMap.TryAdd(typeof(TEventType).FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SubscribeToDefaultTopic(Type type)
        {
            _ = Options.EventsToTopicMigrationMap.TryAdd(type.FullName ?? throw new InvalidOperationException(), (TopicToPublishTo, TopicToSubscribeOn));
        }
    }
}