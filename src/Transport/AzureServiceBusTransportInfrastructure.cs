namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Linq;
    using System.Text;
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
        readonly MessageSenderRegistry messageSenderRegistry;
        readonly HostSettings hostSettings;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings, (ReceiveSettings receiveSettings, ServiceBusClient client)[] receivers, ServiceBusClient defaultClient, ServiceBusAdministrationClient administrationClient, NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;

            this.hostSettings = hostSettings;
            this.administrationClient = administrationClient;
            this.namespacePermissions = namespacePermissions;

            messageSenderRegistry = new MessageSenderRegistry(defaultClient);

            Dispatcher = new MessageDispatcher(messageSenderRegistry, transportSettings.TopicName);
            Receivers = receivers.ToDictionary(s => s.receiveSettings.Id, s => CreateMessagePump(s.receiveSettings, s.client));

            WriteStartupDiagnostics(hostSettings.StartupDiagnostic);
        }


        void WriteStartupDiagnostics(StartupDiagnosticEntries startupDiagnostic) =>
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

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, ServiceBusClient client) =>
            new MessagePump(
                client,
                administrationClient,
                transportSettings,
                TranslateAddress(receiveSettings.ReceiveAddress),
                receiveSettings,
                hostSettings.CriticalErrorAction,
                namespacePermissions);

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            if (messageSenderRegistry != null)
            {
                await messageSenderRegistry.Close(cancellationToken).ConfigureAwait(false);
            }
        }

        public override string ToTransportAddress(QueueAddress address) => TranslateAddress(address);

        // this can be inlined once the TransportDefinition.ToTransportAddress() has been obsoleted with ERROR
        public static string TranslateAddress(QueueAddress address)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append($"-{address.Discriminator}");
            }

            if (address.Qualifier != null)
            {
                queue.Append($".{address.Qualifier}");
            }

            return queue.ToString();
        }
    }
}