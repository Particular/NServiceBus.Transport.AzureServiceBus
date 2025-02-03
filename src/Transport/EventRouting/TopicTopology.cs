#nullable enable

namespace NServiceBus
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.Extensions.Options;

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
                _ => new TopicPerEventTopology(options)
            };

        /// <summary>
        /// 
        /// </summary>
        public static TopicPerEventTopology Default => new(new TopologyOptions());

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
            ValidateOptionsResult validationResult;

            if (Options is MigrationTopologyOptions migrationOptions)
            {
                var migrationOptionsValidator = new MigrationTopologyOptionsValidator();
                validationResult = migrationOptionsValidator.Validate(null, migrationOptions);
            }
            else
            {
                var validator = new TopologyOptionsValidator();
                validationResult = validator.Validate(null, Options);
            }

            if (validationResult.Succeeded)
            {
                return;
            }

            throw new ValidationException(validationResult.FailureMessage);
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