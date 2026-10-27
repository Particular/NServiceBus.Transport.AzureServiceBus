namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

// Hands out a new client per call like the transport, so each forwarder gets its own client
public class FakeForwardingClientFactory
{
    public List<FakeSessionProcessorClient> Clients { get; } = [];

    public Func<Task> StartProcessing { get; set; } = () => Task.CompletedTask;

    public IEnumerable<FakeSessionProcessorClient.FakeSessionProcessor> Processors => Clients.SelectMany(client => client.SubscriptionProcessors);

    public IEnumerable<string> RunningForwarders =>
        Processors.Where(processor => processor.WasStarted && !processor.WasStopped).Select(processor => processor.EntityPath);

    public ServiceBusClient Create()
    {
        var client = new FakeSessionProcessorClient { StartProcessing = () => StartProcessing() };
        Clients.Add(client);
        return client;
    }
}