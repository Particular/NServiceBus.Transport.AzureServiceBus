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

        readonly bool setupInfrastructure;
        readonly ServiceBusAdministrationClient administrationClient;
        readonly string subscribingQueue;
        readonly string subscriptionName;
        readonly AzureServiceBusTransport transportSettings;

        public TopicPerEventTypeTopologySubscriptionManager(
            string subscribingQueue,
            AzureServiceBusTransport transportSettings,
            bool setupInfrastructure,
            ServiceBusAdministrationClient administrationClient)
        {
            this.subscribingQueue = subscribingQueue;
            this.setupInfrastructure = setupInfrastructure;
            this.administrationClient = administrationClient;

            subscriptionName = transportSettings.SubscriptionNamingConvention(subscribingQueue);
            this.transportSettings = transportSettings;
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            if (eventTypes.Length == 0)
            {
                return;
            }

            if (eventTypes.Length == 1)
            {
                await SubscribeEvent(administrationClient, eventTypes[0], cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var subscribeTasks = new List<Task>(eventTypes.Length);
                foreach (var eventType in eventTypes)
                {
                    subscribeTasks.Add(SubscribeEvent(administrationClient, eventType, cancellationToken));
                }
                await Task.WhenAll(subscribeTasks)
                    .ConfigureAwait(false);
            }
        }

        async Task SubscribeEvent(ServiceBusAdministrationClient client, MessageMetadata eventType, CancellationToken cancellationToken)
        {
            // TODO: There is no convention nor mapping here currently.
            // TODO: Is it a good idea to use the subscriptionName as the endpoint name?
            string topicName = eventType.MessageType.FullName.Replace("+", ".");

            if (setupInfrastructure)
            {
                var topicOptions = new CreateTopicOptions(topicName)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMegabytes = transportSettings.EntityMaximumSizeInMegabytes
                };

                try
                {
                    await client.CreateTopicAsync(topicOptions, cancellationToken).ConfigureAwait(false);
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
                    // TODO: Check the log level
                    Logger.WarnFormat("Topic {0} could not be created. Reason: {1}", topicOptions.Name, unauthorizedAccessException.Message);
                    throw;
                }
            }

            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
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
                await client.CreateSubscriptionAsync(subscriptionOptions, cancellationToken).ConfigureAwait(false);
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

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default)
        {
            try
            {
                // TODO: There is no convention nor mapping here currently.
                // TODO: Is it a good idea to use the subscriptionName as the endpoint name?
                await administrationClient.DeleteSubscriptionAsync(eventType.MessageType.FullName, subscriptionName, cancellationToken).ConfigureAwait(false);
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