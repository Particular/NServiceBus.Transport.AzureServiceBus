﻿namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using Azure.Messaging.ServiceBus;

    class NativeMessageCustomizer
    {
        ConcurrentDictionary<string, Action<ServiceBusMessage>> customizations;
        public ConcurrentDictionary<string, Action<Message>> Customizations => customizations ??= new ConcurrentDictionary<string, Action<ServiceBusMessage>>();
    }
}