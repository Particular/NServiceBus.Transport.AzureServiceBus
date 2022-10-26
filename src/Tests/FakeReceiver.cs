namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeReceiver : ServiceBusReceiver
    {
        readonly List<(ServiceBusReceivedMessage, IDictionary<string, object> propertiesToModify)> abandonedMessages = new();
        readonly List<ServiceBusReceivedMessage> completedMessages = new();

        public IReadOnlyCollection<(ServiceBusReceivedMessage, IDictionary<string, object> propertiesToModify)> AbandonedMessages
            => abandonedMessages;

        public IReadOnlyCollection<ServiceBusReceivedMessage> CompletedMessages
            => completedMessages;

        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object> propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            abandonedMessages.Add((message, propertiesToModify ?? new Dictionary<string, object>(0)));
            return Task.CompletedTask;
        }

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            completedMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}