namespace NServiceBus
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.Extensions.Options;
    using NServiceBus.Transport;
    using Particular.Obsoletes;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Represents the topic topology used by <see cref="AzureServiceBusTransport"/>.
    /// </summary>
    /// <remarks>This class cannot be inherited from to create custom topologies. This is a scenario that is currently not supported.</remarks>
    public abstract partial class TopicTopology
    {
        /// <summary>
        /// Allows creating topology instances based on serializable state
        /// </summary>
        protected TopicTopology(TopologyOptions options, IValidateOptions<TopologyOptions> optionsValidator)
        {
            Options = options;
            OptionsValidator = optionsValidator;
        }

        /// <summary>
        /// Gets or sets the options validator used to validate the consistency of the provided options.
        /// </summary>
        public IValidateOptions<TopologyOptions> OptionsValidator { get; set; }

        internal TopologyOptions Options { get; }

        /// <summary>
        /// Creates an instance of the topology object based on serializable state.
        /// </summary>
        /// <param name="options">Serializable topology configuration.</param>
        public static TopicTopology FromOptions(TopologyOptions options) =>
            options switch
            {
#pragma warning disable CS0618 // Type or member is obsolete
                MigrationTopologyOptions migrationOptions => new MigrationTopology(migrationOptions),
#pragma warning restore CS0618 // Type or member is obsolete
                _ => new TopicPerEventTopology(options)
            };

        /// <summary>
        /// Returns the default topology that uses topic per event type.
        /// </summary>
        public static TopicPerEventTopology Default => new(new TopologyOptions());

        /// <summary>
        /// Returns a migration topology using a single topic named <c>bundle-1</c> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        [ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
        [Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
        public static MigrationTopology MigrateFromSingleDefaultTopic() => MigrateFromNamedSingleTopic("bundle-1");

        /// <summary>
        /// Returns a migration topology using a single topic with the <paramref name="topicName"/> for <see cref="MigrationTopology.TopicToPublishTo"/> and <see cref="MigrationTopology.TopicToSubscribeOn"/>
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        [ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
        [Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
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
        [ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
        [Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
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
            ValidateOptionsResult validationResult = OptionsValidator.Validate(null, Options);

            if (!validationResult.Succeeded)
            {
                throw new ValidationException(validationResult.FailureMessage);
            }

            var compatibilityFailure = ValidateMultiplexingCompatibility();
            if (compatibilityFailure != null)
            {
                throw new ValidationException(compatibilityFailure);
            }
        }

        internal string GetPublishDestination(Type eventType)
        {
            var eventTypeFullName = eventType.FullName ?? throw new InvalidOperationException("Message type full name is null");
            return GetPublishDestinationCore(eventTypeFullName);
        }

        internal PublishMultiplexingMode GetPublishMultiplexingMode(string eventTypeFullName)
        {
            if (Options.MultiplexingPublishOptionsMap.TryGetValue(eventTypeFullName, out var options) && options.Mode != PublishMultiplexingMode.Default)
            {
                return options.Mode;
            }

            return Options.DefaultPublishMultiplexingMode;
        }

        internal SubscriptionFilterMode GetSubscriptionFilterMode(SubscriptionEntry entry)
        {
            return entry.FilterMode == SubscriptionFilterMode.Default ? Options.DefaultSubscriptionFilterMode : entry.FilterMode;
        }

        string? ValidateMultiplexingCompatibility()
        {
            if (!AreCompatible(Options.DefaultPublishMultiplexingMode, Options.DefaultSubscriptionFilterMode))
            {
                return $"Default publish multiplexing mode '{Options.DefaultPublishMultiplexingMode}' is incompatible with default subscription filter mode '{Options.DefaultSubscriptionFilterMode}'.";
            }

            foreach (var (eventTypeFullName, topicName) in Options.PublishedEventToTopicsMap)
            {
                if (!Options.SubscribedEventToTopicsMap.TryGetValue(eventTypeFullName, out var subscriptionEntries))
                {
                    continue;
                }

                var effectivePublishMode = GetPublishMultiplexingMode(eventTypeFullName);
                foreach (var entry in subscriptionEntries)
                {
                    var effectiveSubscriptionMode = GetSubscriptionFilterMode(entry);
                    if (!AreCompatible(effectivePublishMode, effectiveSubscriptionMode))
                    {
                        return $"Event '{eventTypeFullName}' on topic '{topicName}' has effective publish mode '{effectivePublishMode}' which is incompatible with effective subscription filter mode '{effectiveSubscriptionMode}'.";
                    }
                }
            }

            return null;
        }

        static bool AreCompatible(PublishMultiplexingMode publishMode, SubscriptionFilterMode subscriptionMode) =>
            publishMode switch
            {
                PublishMultiplexingMode.NotMultiplexed => subscriptionMode == SubscriptionFilterMode.CatchAll,
                PublishMultiplexingMode.MultiplexedUsingCorrelationFilter => subscriptionMode is SubscriptionFilterMode.CorrelationFilter or SubscriptionFilterMode.CatchAll,
                PublishMultiplexingMode.MultiplexedUsingSqlFilter => subscriptionMode is SubscriptionFilterMode.SqlFilter or SubscriptionFilterMode.CatchAll,
                PublishMultiplexingMode.Default => false,
                _ => false
            };

        // By having this internal abstract method it is not possible to extend the topology with a custom topology outside
        // of this assembly. That is a deliberate design decision.
        internal abstract SubscriptionManager CreateSubscriptionManager(SubscriptionManagerCreationOptions creationOptions, HostSettings hostSettings);

        /// <summary>
        /// Returns instructions where to publish a given event.
        /// </summary>
        /// <param name="eventTypeFullName">Full type name of the event.</param>
        protected abstract string GetPublishDestinationCore(string eventTypeFullName);
    }
}
