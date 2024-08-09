#nullable enable
namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeProcessor : ServiceBusProcessor
    {
        readonly ConditionalWeakTable<ServiceBusReceivedMessage, CustomProcessMessageEventArgs>
            receivedMessageToEventArgs = [];
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

        public Task ProcessMessage(ServiceBusReceivedMessage message, ServiceBusReceiver? receiver = null, CancellationToken cancellationToken = default)
        {
            var eventArgs = new CustomProcessMessageEventArgs(message, receiver ?? new FakeReceiver(), cancellationToken);
            receivedMessageToEventArgs.Add(message, eventArgs);
            return OnProcessMessageAsync(eventArgs);
        }

        public Task RaiseMessageLockLost(ServiceBusReceivedMessage message, MessageLockLostEventArgs args, CancellationToken cancellationToken = default)
            => receivedMessageToEventArgs.TryGetValue(message, out var eventArgs) ? eventArgs.RaiseMessageLockLost(args, cancellationToken) : Task.FromCanceled(cancellationToken);

        sealed class CustomProcessMessageEventArgs : ProcessMessageEventArgs
        {
            public CustomProcessMessageEventArgs(ServiceBusReceivedMessage message, ServiceBusReceiver receiver, CancellationToken cancellationToken) : base(message, receiver, cancellationToken)
            {
            }

            public CustomProcessMessageEventArgs(ServiceBusReceivedMessage message, ServiceBusReceiver receiver, string identifier, CancellationToken cancellationToken) : base(message, receiver, identifier, cancellationToken)
            {
            }

            public Task RaiseMessageLockLost(MessageLockLostEventArgs args, CancellationToken cancellationToken = default) => OnMessageLockLostAsync(args);
        }
    }
}