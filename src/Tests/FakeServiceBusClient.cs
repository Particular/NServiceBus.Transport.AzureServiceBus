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

        public override ServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions options)
        {
            if (!Senders.ContainsKey(queueOrTopicName))
            {
                var fakeSender = new FakeSender();
                Senders[queueOrTopicName] = fakeSender;
            }
            return Senders[queueOrTopicName];
        }
    }
}