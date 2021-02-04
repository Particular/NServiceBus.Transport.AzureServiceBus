﻿namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Transport;

    sealed class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly AzureServiceBusTransport transportSettings;

        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        MessageSenderPool messageSenderPool;
        readonly HostSettings hostSettings;
        readonly QueueCreator queueCreator;

        public AzureServiceBusTransportInfrastructure(AzureServiceBusTransport transportSettings, HostSettings hostSettings)
        {
            this.transportSettings = transportSettings;
            this.hostSettings = hostSettings;

            connectionStringBuilder = new ServiceBusConnectionStringBuilder(transportSettings.ConnectionString)
            {
                TransportType =
                transportSettings.UseWebSockets ? TransportType.AmqpWebSockets : TransportType.Amqp
            };

            namespacePermissions = new NamespacePermissions(connectionStringBuilder, transportSettings.TokenProvider);

            messageSenderPool = new MessageSenderPool(connectionStringBuilder, transportSettings.TokenProvider, transportSettings.RetryPolicy);

            Dispatcher = new MessageDispatcher(messageSenderPool, transportSettings.TopicName);

            queueCreator = new QueueCreator(transportSettings, connectionStringBuilder, namespacePermissions);

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

        public override async Task DisposeAsync()
        {
            if (messageSenderPool != null)
            {
                await messageSenderPool.Close().ConfigureAwait(false);
            }
        }

        public async Task Initialize(ReceiveSettings[] receivers, string[] sendingAddresses)
        {
            Receivers = Array.AsReadOnly(receivers.Select(CreateMessagePump).ToArray());

            var allQueues = receivers
                .Select(r => r.ReceiveAddress)
                .Concat(sendingAddresses)
                .ToArray();

            await queueCreator.CreateQueues(allQueues).ConfigureAwait(false);
        }
    }
}