namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.ServiceBus;

    class NativeMessageCustomizer
    {
        ConcurrentDictionary<string, Action<Message>> customizations;

        public ConcurrentDictionary<string, Action<Message>> Customizations => customizations ??= new ConcurrentDictionary<string, Action<Message>>();
    }
}