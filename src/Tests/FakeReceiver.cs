namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    public class FakeReceiver : ServiceBusReceiver
    {
        readonly List<(ServiceBusReceivedMessage, IDictionary<string, object> propertiesToModify)> abandonedMessages = [];
        readonly List<ServiceBusReceivedMessage> completedMessages = [];
        readonly List<ServiceBusReceivedMessage> completingMessages = [];

        public Func<ServiceBusReceivedMessage, CancellationToken, Task> CompleteMessageCallback = (_, _) => Task.CompletedTask;

        public IReadOnlyCollection<(ServiceBusReceivedMessage, IDictionary<string, object> propertiesToModify)> AbandonedMessages
            => abandonedMessages;

        public IReadOnlyCollection<ServiceBusReceivedMessage> CompletedMessages
            => completedMessages;

        public IReadOnlyCollection<ServiceBusReceivedMessage> CompletingMessages
            => completingMessages;

        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object> propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            abandonedMessages.Add((message, propertiesToModify ?? new Dictionary<string, object>(0)));
            return Task.CompletedTask;
        }

        public override async Task CompleteMessageAsync(ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            completingMessages.Add(message);
            await CompleteMessageCallback(message, cancellationToken);
            completedMessages.Add(message);
        }
    }
}