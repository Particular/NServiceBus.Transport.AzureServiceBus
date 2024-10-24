using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Transport;

/// <summary>
/// Allow to customize the native ServiceBusMessage just before it is dispatched to the Azure Service Bus SDK client
/// </summary>
public delegate Task NativeMessageCustomizerCallback(IOutgoingTransportOperation message, ServiceBusMessage nativeMessage);