namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Transport;

    sealed class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly AzureServiceBusTransport transportSettings;

        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        readonly MessageSenderPool messageSenderPool;
        readonly HostSettings hostSettings;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings, ReceiveSettings[] receivers, ServiceBusConnectionStringBuilder connectionStringBuilder, NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.hostSettings = hostSettings;
            this.connectionStringBuilder = connectionStringBuilder;
            this.namespacePermissions = namespacePermissions;

            messageSenderPool = new MessageSenderPool(connectionStringBuilder, transportSettings.TokenProvider, transportSettings.RetryPolicy);

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
                CustomTokenProvider = transportSettings.TokenProvider?.ToString() ?? "default",
                CustomRetryPolicy = transportSettings.RetryPolicy?.ToString() ?? "default"
            });
        }

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings)
        {
            return new MessagePump(
                connectionStringBuilder,
                transportSettings,
                receiveSettings,
                hostSettings.CriticalErrorAction,
                namespacePermissions);
        }

        public override async Task Shutdown(CancellationToken cancellationToken)
        {
            if (messageSenderPool != null)
            {
                await messageSenderPool.Close().ConfigureAwait(false);
            }
        }
    }
}