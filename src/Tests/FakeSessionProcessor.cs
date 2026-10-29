#nullable enable

namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeSessionProcessor(ServiceBusClient client, string queueName, ServiceBusSessionProcessorOptions options)
        : ServiceBusSessionProcessor(client, queueName, options)
    {
        public override Task StartProcessingAsync(CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        public override Task StopProcessingAsync(CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        public Task RaiseError(ProcessErrorEventArgs args) => OnProcessErrorAsync(args);
    }
}
