namespace NServiceBus;

using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

sealed class NativeMessageCustomizer
{
    public ConcurrentDictionary<string, Action<ServiceBusMessage>> Customizations => field ??= new ConcurrentDictionary<string, Action<ServiceBusMessage>>();
}