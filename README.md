# NServiceBus.Transport.AzureServiceBus

NServiceBus.Transport.AzureServiceBus enables the use of the [Azure Service Bus Brokered Messaging](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview) service as the underlying transport used by NServiceBus. This transport uses the [Azure.Messaging.ServiceBus NuGet package](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/).

It is part of the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems.

See the [Azure Service Bus Transport documentation](https://docs.particular.net/transports/azure-service-bus/) for more details on how to use it.

## Running tests locally

### Acceptance Tests

Follow these steps to run the acceptance tests locally:

* Add a new environment variable `AzureServiceBus_ConnectionString` containing a connection string to your Azure Service Bus namespace.
* Add a new environment variable `AzureServiceBus_ConnectionString_Restricted` containing a connection string to the same namespace with [`Send` and `Listen` rights](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas#shared-access-authorization-policies) only.

### Unit Tests

* Add a new environment variable `AzureServiceBus_ConnectionString` containing a connection string to your Azure Service Bus namespace (can be same as for acceptance tests).
