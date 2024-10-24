using Azure.Messaging.ServiceBus;
using NServiceBus.Transport;

/// <summary>
/// Delegate used to customize the native ServiceBusProcessorOptions
/// </summary>
public delegate void CustomizeServiceBusProcessorOptions(ReceiveSettings receiveSettings, ServiceBusProcessorOptions processorOptions);