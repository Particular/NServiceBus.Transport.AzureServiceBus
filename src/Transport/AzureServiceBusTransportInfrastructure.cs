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
        private readonly AzureServiceBusTransport transportSettings;

        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        MessageSenderPool messageSenderPool;
        private HostSettings hostSettings;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings)
        {
            this.transportSettings = transportSettings;
            this.hostSettings = hostSettings;

            connectionStringBuilder = new ServiceBusConnectionStringBuilder(transportSettings.ConnectionString);

            connectionStringBuilder.TransportType =
                transportSettings.UseWebSockets ? TransportType.AmqpWebSockets : TransportType.Amqp;

            //TODO verify this is null if no user-defined token prover has been provided
            namespacePermissions = new NamespacePermissions(connectionStringBuilder, transportSettings.CustomTokenProvider);

            messageSenderPool = new MessageSenderPool(connectionStringBuilder, transportSettings.CustomTokenProvider, transportSettings.CustomRetryPolicy);

            //TODO should those properties really need to be virtual? Makes things more complicated by extracting all assignments into dedicated methods.
            Dispatcher = new MessageDispatcher(messageSenderPool, transportSettings.TopicName);


            WriteStartupDiagnostics(hostSettings.StartupDiagnostic);
        }


        void WriteStartupDiagnostics(StartupDiagnosticEntries startupDiagnostic)
        {
            startupDiagnostic.Add("Azure Service Bus transport", new
            {
                TopicName = transportSettings.TopicName,
                EntityMaximumSize = transportSettings.EntityMaximumSize.ToString(),
                EnablePartitioning = transportSettings.EnablePartitioning.ToString(),
                PrefetchMultiplier = transportSettings.PrefetchMultiplier.ToString(),
                PrefetchCount = transportSettings.PrefetchCount?.ToString() ?? "default",
                UseWebSockets = transportSettings.UseWebSockets.ToString(),
                TimeToWaitBeforeTriggeringCircuitBreaker = transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker.ToString(),
                CustomTokenProvider = transportSettings.CustomTokenProvider?.ToString() ?? "default",
                CustomRetryPolicy = transportSettings.CustomRetryPolicy?.ToString() ?? "default"
            });
        }

        public MessagePump CreateMessagePump(ReceiveSettings receiveSettings)
        {
            return new MessagePump(
                connectionStringBuilder, 
                transportSettings,
                receiveSettings,
                hostSettings.CriticalErrorAction);
        }

        //TODO create queues if necessary during transport.initialize()
        //QueueCreator CreateQueueCreator()
        //{
        //    string localAddress;

        //    try
        //    {
        //        localAddress = settings.LocalAddress();
        //    }
        //    catch
        //    {
        //        // For TransportTests, LocalAddress() will throw. Construct local address manually.
        //        localAddress = ToTransportAddress(LogicalAddress.CreateLocalAddress(settings.EndpointName(), new Dictionary<string, string>()));
        //    }

        //    return new QueueCreator(localAddress, topicName, connectionStringBuilder, tokenProvider, namespacePermissions, maximumSizeInGB * 1024, enablePartitioning, subscriptionNamingConvention);
        //}

        //TODO move this to the receiver
        SubscriptionManager CreateSubscriptionManager()
        {
            return new SubscriptionManager(
                string.Empty, //TODO provide address
                transportSettings.TopicName, 
                connectionStringBuilder,
                transportSettings.CustomTokenProvider,
                namespacePermissions,
                transportSettings.SubscriptionNamingConvention,
                transportSettings.SubscriptionRuleNamingConvention);
        }

        public override async Task DisposeAsync()
        {
            if (messageSenderPool != null)
            {
                await messageSenderPool.Close().ConfigureAwait(false);
            }
        }
    }
}