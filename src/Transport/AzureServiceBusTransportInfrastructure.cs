#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Transport;

    sealed class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly AzureServiceBusTransport transportSettings;

        readonly MessageSenderRegistry messageSenderRegistry;
        readonly HostSettings hostSettings;
        readonly ServiceBusClient defaultClient;
        readonly (ReceiveSettings receiveSettings, ServiceBusClient client)[] receiveSettingsAndClientPairs;

        public AzureServiceBusTransportInfrastructure(
            AzureServiceBusTransport transportSettings,
            HostSettings hostSettings,
            (ReceiveSettings receiveSettings, ServiceBusClient client)[] receiveSettingsAndClientPairs,
            ServiceBusClient defaultClient
            )
        {
            this.transportSettings = transportSettings;

            this.hostSettings = hostSettings;
            this.defaultClient = defaultClient;
            this.receiveSettingsAndClientPairs = receiveSettingsAndClientPairs;

            messageSenderRegistry = new MessageSenderRegistry(defaultClient);

            Dispatcher = new MessageDispatcher(
                messageSenderRegistry,
                transportSettings.Topology.TopicToPublishTo,
                transportSettings.OutgoingNativeMessageCustomization,
                transportSettings.DoNotSendTransportEncodingHeader
                );
            Receivers = receiveSettingsAndClientPairs.ToDictionary(static settingsAndClient =>
            {
                var (receiveSettings, _) = settingsAndClient;
                return receiveSettings.Id;
            }, settingsAndClient =>
            {
                (ReceiveSettings receiveSettings, ServiceBusClient receiveClient) = settingsAndClient;
                return CreateMessagePump(receiveSettings, receiveClient);
            });

            WriteStartupDiagnostics(hostSettings.StartupDiagnostic);
        }

        void WriteStartupDiagnostics(StartupDiagnosticEntries startupDiagnostic) =>
            startupDiagnostic.Add("Azure Service Bus transport", new
            {
                transportSettings.Topology,
                EntityMaximumSize = transportSettings.EntityMaximumSize.ToString(),
                EnablePartitioning = transportSettings.EnablePartitioning.ToString(),
                PrefetchMultiplier = transportSettings.PrefetchMultiplier.ToString(),
                PrefetchCount = transportSettings.PrefetchCount?.ToString() ?? "default",
                UseWebSockets = transportSettings.UseWebSockets.ToString(),
                TimeToWaitBeforeTriggeringCircuitBreaker = transportSettings.TimeToWaitBeforeTriggeringCircuitBreaker.ToString(),
                CustomTokenProvider = transportSettings.TokenCredential?.ToString() ?? "default",
                CustomRetryPolicy = transportSettings.RetryPolicyOptions?.ToString() ?? "default"
            });

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, ServiceBusClient receiveClient)
        {
            string receiveAddress = ToTransportAddress(receiveSettings.ReceiveAddress);
            SubQueue subQueue = ToSubQueue(receiveSettings.ReceiveAddress);

            return new MessagePump(
                receiveClient,
                transportSettings,
                receiveAddress,
                receiveSettings,
                hostSettings.CriticalErrorAction,
                receiveSettings.UsePublishSubscribe
                    ? new SubscriptionManager(receiveAddress, transportSettings, defaultClient)
                    : null,
                subQueue
                );
        }

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(Receivers.Values.Select(r => r.StopReceive(cancellationToken)))
                .ConfigureAwait(false);

            await messageSenderRegistry.Close(cancellationToken).ConfigureAwait(false);

            foreach (var (_, serviceBusClient) in receiveSettingsAndClientPairs)
            {
                await serviceBusClient.DisposeAsync().ConfigureAwait(false);
            }

            await defaultClient.DisposeAsync().ConfigureAwait(false);
        }

        public override string ToTransportAddress(QueueAddress address)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append($"-{address.Discriminator}");
            }

            if (address.Qualifier != null && !QueueAddressQualifier.DeadLetterQueue.Equals(address.Qualifier, StringComparison.OrdinalIgnoreCase))
            {
                queue.Append($".{address.Qualifier}");
            }

            return queue.ToString();
        }

        static SubQueue ToSubQueue(QueueAddress address) =>
            QueueAddressQualifier.DeadLetterQueue.Equals(address.Qualifier, StringComparison.OrdinalIgnoreCase)
                ? SubQueue.DeadLetter
                : SubQueue.None;
    }
}