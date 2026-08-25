namespace NServiceBus.Transport.AzureServiceBus;

using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

sealed class SubscriptionManagerCreationOptions
{
    public required string SubscribingQueueName { get; init; }

    public required ServiceBusAdministrationClient AdministrationClient { get; init; }

    public required ServiceBusClient Client { get; init; }

    public Func<ServiceBusClient>? ForwarderClientFactory { get; init; }

    public bool EnablePartitioning { get; init; }

    public bool RequiresSession { get; init; }

    public int EntityMaximumSizeInMegabytes { get; init; }

    public bool SetupInfrastructure { get; init; }

    internal int MaxDeliveryCount { get; init; } = int.MaxValue;
}