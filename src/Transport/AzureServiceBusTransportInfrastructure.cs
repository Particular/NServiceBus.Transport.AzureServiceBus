namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using DelayedDelivery;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        const string defaultTopicName = "bundle-1";
        static readonly Func<string, string> defaultNameShortener = name => name;
        static readonly Func<string, string> defaultSubscriptionNamingConvention = name => name;
        static readonly Func<Type, string> defaultSubscriptionRuleNamingConvention = type => type.FullName;

        readonly SettingsHolder settings;
        readonly ServiceBusAdministrationClient administrationClient;
        readonly string connectionString;
        readonly string topicName;
        readonly NamespacePermissions namespacePermissions;
        readonly ServiceBusTransportType transportType = ServiceBusTransportType.AmqpTcp;
        readonly TokenCredential tokenCredential;
        readonly ServiceBusRetryOptions retryOptions;
        MessageSenderPool messageSenderPool;

        public AzureServiceBusTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;

            if (!settings.TryGet(SettingsKeys.TopicName, out topicName))
            {
                topicName = defaultTopicName;
            }

            _ = settings.TryGet(SettingsKeys.ServiceBusTransportType, out ServiceBusTransportType tt);
            transportType = tt;

            _ = settings.TryGet(SettingsKeys.CustomRetryPolicy, out ServiceBusRetryOptions ro);
            retryOptions = ro;

            _ = settings.TryGet(SettingsKeys.CustomTokenCredential, out TokenCredential tc);
            tokenCredential = tc;

            administrationClient = tokenCredential != null
                ? new ServiceBusAdministrationClient(connectionString, tokenCredential)
                : new ServiceBusAdministrationClient(connectionString);

            namespacePermissions = new NamespacePermissions(administrationClient);

            WriteStartupDiagnostics();
        }

        //Hack: MessageSenderPool needs a default client instance, to reate one the endpoint transaction mode is needed.
        //In Core v7 endpoint transaction mode is available only at message pump Init time. The following code performs
        //the same steps Core v7 does to get the required transaction mode
        TransportTransactionMode GetRequiredTransactionMode(SettingsHolder settings)
        {
            var transportTransactionSupport = TransactionMode;

            //if user haven't asked for a explicit level use what the transport supports
            if (!settings.HasSetting<TransportTransactionMode>())
            {
                return transportTransactionSupport;
            }

            var requestedTransportTransactionMode = settings.Get<TransportTransactionMode>();

            if (requestedTransportTransactionMode > transportTransactionSupport)
            {
                throw new Exception($"Requested transaction mode `{requestedTransportTransactionMode}` can't be satisfied since the transport only supports `{transportTransactionSupport}`");
            }

            return requestedTransportTransactionMode;
        }

        void WriteStartupDiagnostics()
        {
            settings.AddStartupDiagnosticsSection("Azure Service Bus transport", new
            {
                TopicName = settings.TryGet(SettingsKeys.TopicName, out string customTopicName) ? customTopicName : "default",
                EntityMaximumSize = settings.TryGet(SettingsKeys.MaximumSizeInGB, out int entityMaxSize) ? entityMaxSize.ToString() : "default",
                EnablePartitioning = settings.TryGet(SettingsKeys.EnablePartitioning, out bool enablePartitioning) ? enablePartitioning.ToString() : "default",
                SubscriptionNameShortener = settings.TryGet(SettingsKeys.SubscriptionNameShortener, out Func<string, string> _) ? "configured" : "default",
                RuleNameShortener = settings.TryGet(SettingsKeys.RuleNameShortener, out Func<string, string> _) ? "configured" : "default",
                SubscriptionNamingConvention = settings.TryGet(SettingsKeys.SubscriptionNamingConvention, out Func<string, string> _) ? "configured" : "default",
                SubscriptionRuleNamingConvention = settings.TryGet(SettingsKeys.SubscriptionRuleNamingConvention, out Func<string, string> _) ? "configured" : "default",
                PrefetchMultiplier = settings.TryGet(SettingsKeys.PrefetchMultiplier, out int prefetchMultiplier) ? prefetchMultiplier.ToString() : "default",
                PrefetchCount = settings.TryGet(SettingsKeys.PrefetchCount, out int? prefetchCount) ? prefetchCount.ToString() : "default",
                UseWebSockets = settings.TryGet(SettingsKeys.ServiceBusTransportType, out ServiceBusTransportType _) ? "True" : "default",
                TimeToWaitBeforeTriggeringCircuitBreaker = settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, out TimeSpan timeToWait) ? timeToWait.ToString() : "default",
                CustomTokenProvider = settings.TryGet(SettingsKeys.CustomTokenCredential, out TokenCredential customTokenCredential) ? customTokenCredential.ToString() : "default",
                CustomRetryPolicy = settings.TryGet(SettingsKeys.CustomRetryPolicy, out ServiceBusRetryOptions customRetryPolicy) ? customRetryPolicy.ToString() : "default"
            });
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => CreateMessagePump(),
                () => CreateQueueCreator(),
                () => namespacePermissions.CanReceive());
        }

        MessagePump CreateMessagePump()
        {
            if (!settings.TryGet(SettingsKeys.PrefetchMultiplier, out int prefetchMultiplier))
            {
                prefetchMultiplier = 10;
            }

            settings.TryGet(SettingsKeys.PrefetchCount, out int? prefetchCount);

            if (!settings.TryGet(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, out TimeSpan timeToWaitBeforeTriggeringCircuitBreaker))
            {
                timeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.FromMinutes(2);
            }

            return new MessagePump(connectionString, tokenCredential, prefetchMultiplier, prefetchCount, timeToWaitBeforeTriggeringCircuitBreaker, retryOptions, transportType);
        }

        QueueCreator CreateQueueCreator()
        {
            if (!settings.TryGet(SettingsKeys.MaximumSizeInGB, out int maximumSizeInGB))
            {
                maximumSizeInGB = 5;
            }

            settings.TryGet(SettingsKeys.EnablePartitioning, out bool enablePartitioning);

            if (!settings.TryGet(SettingsKeys.SubscriptionNameShortener, out Func<string, string> subscriptionNameShortener))
            {
                subscriptionNameShortener = defaultNameShortener;
            }

            if (!settings.TryGet(SettingsKeys.SubscriptionNamingConvention, out Func<string, string> subscriptionNamingConvention))
            {
                subscriptionNamingConvention = defaultSubscriptionNamingConvention;
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

            return new QueueCreator(localAddress, topicName, administrationClient, namespacePermissions, maximumSizeInGB * 1024, enablePartitioning, subscriptionNameShortener, subscriptionNamingConvention);
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () => CreateMessageDispatcher(),
                () => namespacePermissions.CanSend());
        }

        MessageDispatcher CreateMessageDispatcher()
        {
            var transactionMode = GetRequiredTransactionMode(settings);
            messageSenderPool = new MessageSenderPool(connectionString, tokenCredential, retryOptions, transportType, transactionMode);

            return new MessageDispatcher(messageSenderPool, topicName);
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => CreateSubscriptionManager());
        }

        SubscriptionManager CreateSubscriptionManager()
        {
            if (!settings.TryGet(SettingsKeys.SubscriptionNameShortener, out Func<string, string> subscriptionNameShortener))
            {
                subscriptionNameShortener = defaultNameShortener;
            }

            if (!settings.TryGet(SettingsKeys.RuleNameShortener, out Func<string, string> ruleNameShortener))
            {
                ruleNameShortener = defaultNameShortener;
            }

            if (!settings.TryGet(SettingsKeys.SubscriptionNamingConvention, out Func<string, string> subscriptionNamingConvention))
            {
                subscriptionNamingConvention = defaultSubscriptionNamingConvention;
            }

            if (!settings.TryGet(SettingsKeys.SubscriptionRuleNamingConvention, out Func<Type, string> ruleNamingConvention))
            {
                ruleNamingConvention = defaultSubscriptionRuleNamingConvention;
            }

            return new SubscriptionManager(settings.LocalAddress(), topicName, administrationClient, namespacePermissions, subscriptionNameShortener, ruleNameShortener, subscriptionNamingConvention, ruleNamingConvention);
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