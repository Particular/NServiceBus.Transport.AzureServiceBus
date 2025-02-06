namespace NServiceBus;

using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

sealed class NativeMessageCustomizer
{
    ConcurrentDictionary<string, Action<ServiceBusMessage>>? customizations;
    public ConcurrentDictionary<string, Action<ServiceBusMessage>> Customizations => customizations ??= new ConcurrentDictionary<string, Action<ServiceBusMessage>>();
}