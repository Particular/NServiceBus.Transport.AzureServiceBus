namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Transport;

    sealed class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly AzureServiceBusTransport transportSettings;

        readonly string connectionString;
        readonly ServiceBusClientOptions serviceBusClientOptions;
        readonly NamespacePermissions namespacePermissions;
        readonly MessageSenderPool messageSenderPool;
        readonly HostSettings hostSettings;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings, ReceiveSettings[] receivers, string connectionString, ServiceBusClientOptions serviceBusClientOptions, NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;

            this.hostSettings = hostSettings;
            this.connectionString = connectionString;
            this.serviceBusClientOptions = serviceBusClientOptions;
            this.namespacePermissions = namespacePermissions;

            messageSenderPool = new MessageSenderPool(connectionString, serviceBusClientOptions, transportSettings.TokenCredential, transportSettings.RetryPolicyOptions);

            Dispatcher = new MessageDispatcher(messageSenderPool, transportSettings.TopicName);
            Receivers = receivers.ToDictionary(s => s.Id, s => CreateMessagePump(s));

            WriteStartupDiagnostics(hostSettings.StartupDiagnostic);
        }


        void WriteStartupDiagnostics(StartupDiagnosticEntries startupDiagnostic)
        {
            startupDiagnostic.Add("Azure Service Bus transport", new
            {
                transportSettings.TopicName,
                EntityMaximumSize = transportSettings.EntityMaximumSize.ToString(),
                EnablePartitioning = transportSettings.EnablePartitioning.ToString(),
                PrefetchMultiplier = transportSettings.PrefetchMultiplier.ToString(),
                PrefetchCount = transportSettings.PrefetchCount?.ToString() ?? "default",
                UseWebSockets = transportSettings.UseWebSockets.ToString(),
                TimeToWaitBeforeTriggeringCircuitBreaker = transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker.ToString(),
                CustomTokenProvider = transportSettings.TokenCredential?.ToString() ?? "default",
                CustomRetryPolicy = transportSettings.RetryPolicyOptions?.ToString() ?? "default"
            });
        }

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings)
        {
            return new MessagePump(
                connectionString,
                serviceBusClientOptions,
                transportSettings,
                receiveSettings,
                hostSettings.CriticalErrorAction,
                namespacePermissions);
        }

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            if (messageSenderPool != null)
            {
                await messageSenderPool.Close(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}