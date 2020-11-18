namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.ServiceBus;

    internal class NativeMessageCustomizer
    {
        private ConcurrentDictionary<string, Action<Message>> customizations;

        public ConcurrentDictionary<string, Action<Message>> Customizations => customizations ?? (customizations = new ConcurrentDictionary<string, Action<Message>>());
    }
}