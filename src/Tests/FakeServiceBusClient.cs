namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Collections.Generic;
    using Azure.Messaging.ServiceBus;

    public class FakeServiceBusClient : ServiceBusClient
    {
        public Dictionary<string, FakeSender> Senders { get; } = new Dictionary<string, FakeSender>();

        public override ServiceBusSender CreateSender(string queueOrTopicName)
        {
            if (!Senders.ContainsKey(queueOrTopicName))
            {
                Senders[queueOrTopicName] = new FakeSender();
            }
            return Senders[queueOrTopicName];
        }
    }
}