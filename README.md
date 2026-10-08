# NServiceBus.Transport.AzureServiceBus

NServiceBus.Transport.AzureServiceBus enables the use of the [Azure Service Bus Brokered Messaging](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview) service as the underlying transport used by NServiceBus. This transport uses the [Azure.Messaging.ServiceBus NuGet package](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/).

It is part of the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems.

See the [Azure Service Bus Transport documentation](https://docs.particular.net/transports/azure-service-bus/) for more details on how to use it.

## Running tests locally

### Acceptance Tests

Follow these steps to run the acceptance tests locally:

* Add a new environment variable `AzureServiceBus_ConnectionString` containing a connection string to your Azure Service Bus namespace.
* Add a new environment variable `AzureServiceBus_ConnectionString_Restricted` containing a connection string to the same namespace with [`Send` and `Listen` rights](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas#shared-access-authorization-policies) only.
* Some tests are using `Azure.Identity` with the `DefaultAzureCredential` and require one of the supported credentials to be present locally. For more information see the [troubleshooting guideline](https://aka.ms/azsdk/net/identity/defaultazurecredential/troubleshoot)

The acceptance and transport test assemblies share one namespace and isolate their entities with a per-assembly prefix. The acceptance test assemblies also run their fixtures in parallel (tests within a fixture stay sequential), so every fixture gets its own namespace segment below the assembly prefix. When a test touches an entity by name through the admin client, wrap the name in `AcceptanceTestEntityNames.For(...)` so it resolves to the running fixture's entity in every assembly the test is linked into.

The `NoHierarchy.AcceptanceTests` assembly is the exception: it runs a small slice of the shared scenarios sequentially and without a hierarchy namespace, so the default entity paths stay covered. Add scenarios to its project file with a `Compile Include`.

### Unit Tests

* Add a new environment variable `AzureServiceBus_ConnectionString` containing a connection string to your Azure Service Bus namespace (can be same as for acceptance tests).
