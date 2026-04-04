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
        readonly List<(ServiceBusReceivedMessage Message, IDictionary<string, object> PropertiesToModify, string DeadLetterReason, string DeadLetterErrorDescription)> deadLetteredMessages = [];

        public Func<ServiceBusReceivedMessage, CancellationToken, Task> CompleteMessageCallback = (_, _) => Task.CompletedTask;

        public IReadOnlyCollection<(ServiceBusReceivedMessage, IDictionary<string, object> propertiesToModify)> AbandonedMessages
            => abandonedMessages;

        public IReadOnlyCollection<ServiceBusReceivedMessage> CompletedMessages
            => completedMessages;

        public IReadOnlyCollection<ServiceBusReceivedMessage> CompletingMessages
            => completingMessages;

        public IReadOnlyCollection<(ServiceBusReceivedMessage Message, IDictionary<string, object> PropertiesToModify, string DeadLetterReason, string DeadLetterErrorDescription)> DeadLetteredMessages
            => deadLetteredMessages;

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

        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object> propertiesToModify = null,
            string deadLetterReason = null, string deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
        {
            deadLetteredMessages.Add((message, propertiesToModify ?? new Dictionary<string, object>(0), deadLetterReason, deadLetterErrorDescription));
            return Task.CompletedTask;
        }
    }
}
