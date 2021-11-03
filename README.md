# Azure Service Bus Transport for NServiceBus

The Azure ServiceBus transport for NServiceBus enables the use of the Azure Service Bus Brokered Messaging service as the underlying transport used by NServiceBus. 
This transport uses the [Azure.Messaging.ServiceBus](https://www.nuget.org/packages/Azure.Messaging.ServiceBus/) NuGet package.

## Documentation

[Azure Service Bus transport](https://docs.particular.net/transports/azure-service-bus/)

[Samples](https://docs.particular.net/transports/azure-service-bus/#related-samples)

## Running the Acceptance Tests

Follow these steps to run the acceptance tests locally:
* Add a new environment variable `AzureServiceBus_ConnectionString` containing a connection string to your Azure Service Bus namespace

## Running the Unit Tests

* Add a new environment variable `AzureServiceBus_ConnectionString`containing a connection string to your Azure Service Bus namespace (could be same as for acceptance tests)
