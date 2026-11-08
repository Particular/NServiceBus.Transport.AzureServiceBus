#nullable enable

namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeSessionProcessor(ServiceBusClient client, string queueName, ServiceBusSessionProcessorOptions options)
        : ServiceBusSessionProcessor(client, queueName, options)
    {
        public override Task StartProcessingAsync(CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        public override Task StopProcessingAsync(CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        public Task RaiseError(ProcessErrorEventArgs args) => OnProcessErrorAsync(args);

        public Task ProcessMessage(ServiceBusReceivedMessage message, FakeSessionReceiver receiver, CancellationToken cancellationToken = default) =>
            OnProcessSessionMessageAsync(new ProcessSessionMessageEventArgs(message, receiver, cancellationToken));
    }

    public class FakeSessionReceiver(string sessionId) : ServiceBusSessionReceiver
    {
        public override string SessionId => sessionId;

        public IReadOnlyCollection<ServiceBusReceivedMessage> AbandonedMessages => abandonedMessages;

        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            abandonedMessages.Add(message);
            return Task.CompletedTask;
        }

        readonly List<ServiceBusReceivedMessage> abandonedMessages = [];
    }
}