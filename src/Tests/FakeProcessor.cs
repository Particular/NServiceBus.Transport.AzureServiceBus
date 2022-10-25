namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeProcessor : ServiceBusProcessor
    {
        public bool WasStarted { get; private set; }
        public bool WasStopped { get; private set; }

        public override Task StartProcessingAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            WasStarted = true;
            return Task.CompletedTask;
        }

        public override Task StopProcessingAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            WasStopped = true;
            return Task.CompletedTask;
        }

        public Task ProcessMessage(ServiceBusReceivedMessage message, ServiceBusReceiver receiver = null, CancellationToken cancellationToken = default)
            => OnProcessMessageAsync(new ProcessMessageEventArgs(message, receiver ?? new FakeReceiver(), cancellationToken));
    }
}