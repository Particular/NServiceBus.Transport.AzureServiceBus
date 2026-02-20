namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Extensibility;
using Logging;
using Unicast.Messages;

sealed class TopicPerEventTopologySubscriptionManager : SubscriptionManager
{
    readonly TopologyOptions topologyOptions;
    readonly string subscriptionName;

    public TopicPerEventTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions,
        TopologyOptions topologyOptions) : base(creationOptions)
    {
        this.topologyOptions = topologyOptions;
        subscriptionName = topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(CreationOptions.SubscribingQueueName, CreationOptions.SubscribingQueueName);
    }

    static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTopologySubscriptionManager>();

    public override Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context,
        CancellationToken cancellationToken = default) =>
        eventTypes.Length switch
        {
            0 => Task.CompletedTask,
            1 => SubscribeEvent(eventTypes[0].MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"), cancellationToken),
            _ => Task.WhenAll(eventTypes.Select(eventType =>
                    SubscribeEvent(eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null"), cancellationToken))
                .ToArray())
        };

    Task SubscribeEvent(string eventTypeFullName, CancellationToken cancellationToken)
    {
        var topics = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        return CreateSubscriptionsForTopics(topics, subscriptionName, CreationOptions, cancellationToken);
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
                MaxDeliveryCount = creationOptions.MaxDeliveryCount,
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