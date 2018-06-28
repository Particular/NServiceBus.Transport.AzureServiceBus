namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        const string defaultTopicName = "bundle-1";

        readonly SettingsHolder settings;
        readonly string connectionString;
        MessageSenderPool messageSenderPool;

        public AzureServiceBusTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => CreateMessagePump(),
                () => CreateQueueCreator(),
                () => NamespacePermissions.CanManage());
        }

        MessagePump CreateMessagePump()
        {
            var builder = new ServiceBusConnectionStringBuilder(connectionString);

            if (settings.TryGet(SettingsKeys.TransportType, out TransportType transportType))
            {
                builder.TransportType = transportType;
            }

            if (!settings.TryGet(SettingsKeys.PrefetchMultiplier, out int prefetchMultiplier))
            {
                prefetchMultiplier = 10;
            }

            settings.TryGet(SettingsKeys.PrefetchCount, out int prefetchCount);

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, out TimeSpan timeToWaitBeforeTriggeringCircuitBreaker))
            {
                timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);
            }

            settings.TryGet(SettingsKeys.CustomTokenProvider, out ITokenProvider tokenProvider);

            return new MessagePump(builder, tokenProvider, prefetchMultiplier, prefetchCount, timeToWaitBeforeTriggeringCircuitBreaker);
        }

        QueueCreator CreateQueueCreator()
        {
            if (!settings.TryGet(SettingsKeys.TopicName, out string topicName))
            {
                topicName = defaultTopicName;
            }

            if (!settings.TryGet(SettingsKeys.MaximumSizeInGB, out int maximumSizeInGB))
            {
                maximumSizeInGB = 5;
            }

            settings.TryGet(SettingsKeys.EnablePartitioning, out bool enablePartitioning);

            if (!settings.TryGet(SettingsKeys.SubscriptionNameShortener, out Func<string, string> subscriptionNameShortener))
            {
                subscriptionNameShortener = subscriptionName => subscriptionName;
            }

            string localAddress;

            try
            {
                localAddress = settings.LocalAddress();
            }
            catch
            {
                // For TransportTests, LocalAddress() will throw. Construct local address manually.
                localAddress = ToTransportAddress(LogicalAddress.CreateLocalAddress(settings.EndpointName(), new Dictionary<string, string>()));
            }

            return new QueueCreator(localAddress, topicName, connectionString, maximumSizeInGB * 1024, enablePartitioning, subscriptionNameShortener);
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () => CreateMessageDispatcher(),
                () => NamespacePermissions.CanSend());
        }

        MessageDispatcher CreateMessageDispatcher()
        {
            if (!settings.TryGet(SettingsKeys.TopicName, out string topicName))
            {
                topicName = defaultTopicName;
            }

            var builder = new ServiceBusConnectionStringBuilder(connectionString);

            if (settings.TryGet(SettingsKeys.TransportType, out TransportType transportType))
            {
                builder.TransportType = transportType;
            }

            settings.TryGet(SettingsKeys.CustomTokenProvider, out ITokenProvider tokenProvider);

            messageSenderPool = new MessageSenderPool(builder, tokenProvider);

            return new MessageDispatcher(messageSenderPool, topicName);
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => CreateSubscriptionManager());
        }

        SubscriptionManager CreateSubscriptionManager()
        {
            if (!settings.TryGet(SettingsKeys.TopicName, out string topicName))
            {
                topicName = defaultTopicName;
            }

            if (!settings.TryGet(SettingsKeys.SubscriptionNameShortener, out Func<string, string> subscriptionNameShortener))
            {
                subscriptionNameShortener = subscriptionName => subscriptionName;
            }

            if (!settings.TryGet(SettingsKeys.RuleNameShortener, out Func<string, string> ruleNameShortener))
            {
                ruleNameShortener = ruleName => ruleName;
            }

            return new SubscriptionManager(settings.LocalAddress(), topicName, connectionString, subscriptionNameShortener, ruleNameShortener);
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance) => instance;

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint);

            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append($"-{logicalAddress.EndpointInstance.Discriminator}");
            }

            if (logicalAddress.Qualifier != null)
            {
                queue.Append($".{logicalAddress.Qualifier}");
            }

            return queue.ToString();
        }

        public override Task Start()
        {
            return base.Start();
        }

        public override async Task Stop()
        {
            if (messageSenderPool != null)
            {
                await messageSenderPool.Close().ConfigureAwait(false);
            }
        }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type>
        {
            typeof(DelayDeliveryWith),
            typeof(DoNotDeliverBefore),
            typeof(DiscardIfNotReceivedBefore)
        };

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.SendsAtomicWithReceive;

        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
    }
}