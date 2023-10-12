namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Collections.Generic;
    using Azure.Messaging.ServiceBus;

    public class FakeServiceBusClient : ServiceBusClient
    {
        public Dictionary<string, FakeSender> Senders { get; } = [];
        public Dictionary<string, FakeProcessor> Processors { get; } = [];

        public override ServiceBusSender CreateSender(string queueOrTopicName)
        {
            if (!Senders.TryGetValue(queueOrTopicName, out var fakeSender))
            {
                fakeSender = new FakeSender();
                Senders.Add(queueOrTopicName, fakeSender);
            }
            return fakeSender;
        }

        public override ServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions options)
        {
            if (!Senders.TryGetValue(queueOrTopicName, out var fakeSender))
            {
                fakeSender = new FakeSender();
                Senders.Add(queueOrTopicName, fakeSender);
            }
            return fakeSender;
        }

        public override ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
        {
            if (!Processors.TryGetValue(queueName, out var fakeProcessor))
            {
                fakeProcessor = new FakeProcessor();
                Processors.Add(queueName, fakeProcessor);
            }
            return fakeProcessor;
        }
    }
}