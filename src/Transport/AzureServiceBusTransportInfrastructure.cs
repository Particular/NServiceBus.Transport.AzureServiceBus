namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;

    class AzureServiceBusTransportInfrastructure : TransportInfrastructure
    {
        MessageSenderPool messageSenderPool;

        public AzureServiceBusTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            messageSenderPool = new MessageSenderPool(connectionString);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            // TODO: check that we have Receive rights
            
            return new TransportReceiveInfrastructure(
                () => new MessagePump(),
                () => new QueueCreator(),  // TODO: check for Manage Rights
                () => Task.FromResult(StartupCheckResult.Success));
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
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager());
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

        // TODO: Change to SendsAtomicWithReceive when https://github.com/Azure/azure-service-bus-dotnet/issues/423 is closed
        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override OutboundRoutingPolicy OutboundRoutingPolicy => new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
    }
}