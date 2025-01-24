#nullable enable

namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Text;

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
                    QueueNameToSubscriptionNameMap = options.QueueNameToSubscriptionNameMap
                })
            };

        /// <summary>
        /// 
        /// </summary>
        public static TopicPerEventTopology Default => new(new TopicPerEventTopologyOptions());

        /// <summary>
        /// Returns a migration topology using a single topic named <c>bundle-1</c> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        public static MigrationTopology MigrateFromSingleDefaultTopic() => MigrateFromNamedSingleTopic("bundle-1");

        /// <summary>
        /// Returns a migration topology using a single topic with the <paramref name="topicName"/> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        public static MigrationTopology MigrateFromNamedSingleTopic(string topicName) => new(new MigrationTopologyOptions
        {
            TopicToPublishTo = topicName,
            TopicToSubscribeOn = topicName,
        });

        /// <summary>
        /// Returns a migration topology using a distinct name for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicToPublishTo">The topic name to publish to.</param>
        /// <param name="topicToSubscribeOn">The topic name to subscribe to.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="topicToPublishTo"/> is equal to <paramref name="topicToSubscribeOn"/>.</exception>
        public static MigrationTopology MigrateFromTopicHierarchy(string topicToPublishTo, string topicToSubscribeOn)
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

        /// <summary>
        /// Validates the topology follows the restrictions of Azure Service Bus.
        /// </summary>
        public void Validate()
        {
            var validationContext = new ValidationContext(Options);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(Options, validationContext, validationResults, validateAllProperties: true))
            {
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Validation failed for the following reasons:");

                foreach (var result in validationResults)
                {
                    // The result.MemberNames list indicates which properties/fields caused the error.
                    var members = string.Join(", ", result.MemberNames);

                    // You can format this however you'd like, e.g. multiple lines, bullet points, etc.
                    messageBuilder.AppendLine(
                        $"- {result.ErrorMessage}" +
                        (string.IsNullOrWhiteSpace(members) ? "" : $" (Members: {members})")
                    );
                }

                throw new ValidationException(messageBuilder.ToString());
            }
        }

        // Should we make those public?
        internal string GetPublishDestination(Type eventType)
        {
            var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            return GetPublishDestinationCore(eventTypeFullName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventTypeFullName"></param>
        /// <param name="subscribingQueueName"></param>
        /// <returns></returns>
        protected abstract (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)[] GetSubscribeDestinationsCore(
            string eventTypeFullName, string subscribingQueueName);

        internal (string Topic, string SubscriptionName, (string RuleName, string RuleFilter)? RuleInfo)[] GetSubscribeDestinations(Type eventType, string subscribingQueueName)
        {
            var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            return GetSubscribeDestinationsCore(eventTypeFullName, subscribingQueueName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventTypeFullName"></param>
        /// <returns></returns>
        protected abstract string GetPublishDestinationCore(string eventTypeFullName);
    }
}