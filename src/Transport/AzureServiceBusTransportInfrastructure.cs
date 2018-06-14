namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Microsoft.Azure.ServiceBus;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        readonly SettingsHolder settings;
        readonly string connectionString;
        readonly MessageSenderPool messageSenderPool;

        public AzureServiceBusTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
            messageSenderPool = new MessageSenderPool(connectionString);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            // TODO: check that we have Receive rights
            
            return new TransportReceiveInfrastructure(
                () => CreateMessagePump(),
                () => new QueueCreator(),  // TODO: check for Manage Rights
                () => Task.FromResult(StartupCheckResult.Success));
        }

        MessagePump CreateMessagePump()
        {
            // TODO: replace hard-coded values with settings
            return new MessagePump(connectionString, TransportType.Amqp, 10, 100, TimeSpan.FromSeconds(30));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            // TODO: check that we have Send rights

            return new TransportSendInfrastructure(
                () => new MessageDispatcher(messageSenderPool),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            // TODO: use topic from settings
            // TODO: use subscription and rule shorteners from settings if registered
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(settings.LocalAddress(), "bundle-1", connectionString, subscriptionName => subscriptionName, ruleName => ruleName));
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
            await messageSenderPool.Close().ConfigureAwait(false);
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