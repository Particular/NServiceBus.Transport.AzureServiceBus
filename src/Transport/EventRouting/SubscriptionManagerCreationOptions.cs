#nullable enable
namespace NServiceBus.Transport.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

sealed class SubscriptionManagerCreationOptions
{
    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public string SubscribingQueueName { get; set; } = null!;

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public bool EnablePartitioning { get; set; }

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public int EntityMaximumSizeInMegabytes { get; set; }

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public bool SetupInfrastructure { get; set; }

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public ServiceBusAdministrationClient AdministrationClient { get; set; } = null!;

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    public ServiceBusClient Client { get; set; } = null!;
}