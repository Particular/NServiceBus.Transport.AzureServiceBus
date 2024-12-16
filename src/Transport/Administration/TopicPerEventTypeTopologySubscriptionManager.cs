namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Extensibility;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class TopicPerEventTypeTopologySubscriptionManager : ISubscriptionManager
    {
        static readonly ILog Logger = LogManager.GetLogger<TopicPerEventTypeTopologySubscriptionManager>();

        readonly NamespacePermissions namespacePermissions;
        readonly string subscribingQueue;
        readonly string subscriptionName;

        public TopicPerEventTypeTopologySubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            NamespacePermissions namespacePermissions)
        {
            this.subscribingQueue = subscribingQueue;
            this.namespacePermissions = namespacePermissions;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            if (eventTypes.Length == 0)
            {
                return;
            }

            ServiceBusAdministrationClient client;
            try
            {
                client = await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCausedBy(cancellationToken))
            {
                return;
            }
            catch (Exception e) when (e.InnerException is UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.InfoFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                return;
            }

            if (eventTypes.Length == 1)
            {
                await SubscribeEvent(client, eventTypes[0], cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var subscribeTasks = new List<Task>(eventTypes.Length);
                foreach (var eventType in eventTypes)
                {
                    subscribeTasks.Add(SubscribeEvent(client, eventType, cancellationToken));
                }
                await Task.WhenAll(subscribeTasks)
                    .ConfigureAwait(false);
            }
        }

        async Task SubscribeEvent(ServiceBusAdministrationClient client, MessageMetadata eventType, CancellationToken cancellationToken)
        {
            // TODO: There is no convention nor mapping here currently.
            // TODO: Is it a good idea to use the subscriptionName as the endpoint name?
            var subscription = new CreateSubscriptionOptions(eventType.MessageType.FullName.Replace("+", "."), subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(5),
                ForwardTo = subscribingQueue,
                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                MaxDeliveryCount = int.MaxValue,
                EnableBatchedOperations = true,
                UserMetadata = subscribingQueue
            };

            try
            {
                await client.CreateSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException createSbe) when (createSbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // ignored due to race conditions
            }
            catch (ServiceBusException sbe) when (sbe.IsTransient)// An operation is in progress.
            {
                Logger.Info($"Default subscription rule for topic {subscription.TopicName} is already in progress");
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.InfoFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            ServiceBusAdministrationClient client;
            try
            {
                client = await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!e.IsCausedBy(cancellationToken))
            {
                return;
            }
            catch (Exception e) when (e.InnerException is UnauthorizedAccessException unauthorizedAccessException)
            {
                Logger.InfoFormat("Subscription {0} could not be created. Reason: {1}", subscriptionName, unauthorizedAccessException.Message);
                return;
            }

            try
            {
                // TODO: There is no convention nor mapping here currently.
                // TODO: Is it a good idea to use the subscriptionName as the endpoint name?
                await client.DeleteSubscriptionAsync(eventType.MessageType.FullName, subscriptionName, cancellationToken).ConfigureAwait(false);
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