#nullable enable

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

sealed class TopicPerEventTypeTopologySubscriptionManager : SubscriptionManager
{
    readonly TopologyOptions topologyOptions;
    readonly string subscriptionName;

    public TopicPerEventTypeTopologySubscriptionManager(SubscriptionManagerCreationOptions creationOptions,
        TopologyOptions topologyOptions) : base(creationOptions)
    {
        this.topologyOptions = topologyOptions;
        subscriptionName = topologyOptions.QueueNameToSubscriptionNameMap.GetValueOrDefault(CreationOptions.SubscribingQueueName, CreationOptions.SubscribingQueueName);
    }

    static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTypeTopologySubscriptionManager>();

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
        return CreateSubscriptionsForTopics(topics, subscriptionName, CreationOptions.SubscribingQueueName, CreationOptions.AdministrationClient, cancellationToken);
    }

    public override Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var eventTypeFullName = eventType.MessageType.FullName ?? throw new InvalidOperationException("Message type full name is null");
        var topics = topologyOptions.SubscribedEventToTopicsMap.GetValueOrDefault(eventTypeFullName, [eventTypeFullName]);
        return DeleteSubscriptionsForTopics(topics, subscriptionName, CreationOptions.AdministrationClient, cancellationToken);
    }

    public static Task CreateSubscriptionsForTopics(HashSet<string> topics,
        string subscriptionName,
        string subscribingQueueName, ServiceBusAdministrationClient administrationClient,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(topics.Select(CreateSubscription).ToArray());

        async Task CreateSubscription(string topicName)
        {
            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = subscribingQueueName,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = subscribingQueueName
            };

            try
            {
                await administrationClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
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
                // TODO: Check the log level
                Logger.WarnFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
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