namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using Transport;

    sealed class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly AzureServiceBusTransport transportSettings;

        readonly ServiceBusAdministrationClient administrationClient;
        readonly NamespacePermissions namespacePermissions;
        readonly MessageSenderPool messageSenderPool;
        readonly HostSettings hostSettings;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings, (ReceiveSettings receiveSettings, ServiceBusClient client)[] receivers, ServiceBusClient defaultClient, ServiceBusAdministrationClient administrationClient, NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;

            this.hostSettings = hostSettings;
            this.administrationClient = administrationClient;
            this.namespacePermissions = namespacePermissions;

            messageSenderPool = new MessageSenderPool(defaultClient);

            Dispatcher = new MessageDispatcher(messageSenderPool, transportSettings.TopicName);
            Receivers = receivers.ToDictionary(s => s.receiveSettings.Id, s => CreateMessagePump(s.receiveSettings, s.client));

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

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, ServiceBusClient client)
        {
            return new MessagePump(
                client,
                administrationClient,
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