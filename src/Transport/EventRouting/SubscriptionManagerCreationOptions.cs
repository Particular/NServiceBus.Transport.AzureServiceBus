namespace NServiceBus.Transport.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

sealed class SubscriptionManagerCreationOptions
{
    public required string SubscribingQueueName { get; init; }

    public required ServiceBusAdministrationClient AdministrationClient { get; init; }

    public required ServiceBusClient Client { get; init; }

    public bool EnablePartitioning { get; init; }

    public int EntityMaximumSizeInMegabytes { get; init; }

    public bool SetupInfrastructure { get; init; }
}