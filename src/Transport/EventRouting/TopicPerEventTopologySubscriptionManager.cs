namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using EventRouting;
using Extensibility;
using Logging;
using Unicast.Messages;

sealed class TopicPerEventTopologySubscriptionManager : SubscriptionManager
{
    readonly TopologyOptions topologyOptions;
    readonly StartupDiagnosticEntries startupDiagnostic;
    readonly string subscriptionName;
    readonly DestinationManager destinationManager;

    public TopicPerEventTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions,
        TopologyOptions topologyOptions,
        StartupDiagnosticEntries startupDiagnostic) : base(creationOptions)
    {
        this.topologyOptions = topologyOptions;
        this.startupDiagnostic = startupDiagnostic;
        subscriptionName = topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(CreationOptions.SubscribingQueueName, CreationOptions.SubscribingQueueName);

        // The subscription name is limited to 50 characters and the hierarchy is respected by the topic name
        // so there is no need to add it to the subscription name.
        destinationManager = new DestinationManager(topologyOptions.HierarchyNamespaceOptions);
        subscriptionName = destinationManager.StripHierarchyNamespace(subscriptionName);
    }

    static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTopologySubscriptionManager>();

    public override Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = eventTypes
            .Select(eventType => eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"))
            .SelectMany(eventTypeFullName => MapEventToDestinationTopics(eventTypeFullName)
                .Select(topicName => new { Topic = topicName.ToLower(), MessageType = eventTypeFullName }))
            .GroupBy(topicAndMessageType => topicAndMessageType.Topic)
            .Select(group => new
            {
                TopicName = group.Key,
                MessageTypes = group.Select(topicAndMessageType => topicAndMessageType.MessageType).ToArray()
            })
            .ToArray();
        startupDiagnostic.Add("Manifest-Subscriptions", subscriptions);

        return eventTypes.Length switch
        {
            0 => Task.CompletedTask,
            1 => SubscribeEvent(eventTypes[0].MessageType.FullName!, cancellationToken),
            _ => Task.WhenAll(eventTypes.Select(eventType =>
                    SubscribeEvent(eventType.MessageType.FullName!, cancellationToken))
                .ToArray())
        };
    }

    Task SubscribeEvent(string eventTypeFullName, CancellationToken cancellationToken)
    {
        var topics = MapEventToDestinationTopics(eventTypeFullName);
        return CreateSubscriptionsForTopics(topics, subscriptionName, CreationOptions, cancellationToken);
    }

    HashSet<string> MapEventToDestinationTopics(string eventTypeFullName)
    {
        var topics = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        topics = [.. topics.Select(t => destinationManager.GetDestination(t, eventTypeFullName))];
        return topics;
    }

    public override Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        var topics = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        return DeleteSubscriptionsForTopics(topics, subscriptionName, CreationOptions.AdministrationClient, cancellationToken);
    }

    public static Task CreateSubscriptionsForTopics(HashSet<string> topics,
        string subscriptionName,
        SubscriptionManagerCreationOptions creationOptions,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(topics.Select(CreateSubscription).ToArray());

        async Task CreateSubscription(string topicName)
        {
            if (creationOptions.SetupInfrastructure)
            {
                var topicOptions = new CreateTopicOptions(topicName)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = creationOptions.EnablePartitioning,
                    MaxSizeInMegabytes = creationOptions.EntityMaximumSizeInMegabytes
                };

                try
                {
                    await creationOptions.AdministrationClient.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    // ignored due to race conditions
                }
                catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
                {
                    Logger.Info($"Topic creation for topic {topicOptions.Name} is already in progress");
                }
                catch (UnauthorizedAccessException unauthorizedAccessException)
                {
                    Logger.ErrorFormat("Topic {0} could not be created. Reason: {1}", topicOptions.Name, unauthorizedAccessException.Message);
                    throw;
                }
            }

            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = creationOptions.SubscribingQueueName,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = creationOptions.SubscribingQueueName
            };

            try
            {
                await creationOptions.AdministrationClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // ignored due to race conditions
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Default subscription creation for topic {subscriptionOptions.TopicName} is already in progress");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.ErrorFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                throw;
            }
        }
    }

    public static Task DeleteSubscriptionsForTopics(HashSet<string> topics, string subscriptionName,
        ServiceBusAdministrationClient administrationClient,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(topics.Select(DeleteSubscription).ToArray());

        async Task DeleteSubscription(string topicName)
        {
            try
            {
                await administrationClient.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.InfoFormat("Subscription {0} could not be deleted. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
            }
        }
    }
}