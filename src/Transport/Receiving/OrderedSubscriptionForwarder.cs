namespace NServiceBus.Transport.AzureServiceBus.Receiving;

using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;

public class OrderedSubscriptionForwarder
{
    readonly ServiceBusClient serviceBusClient;
    readonly string topicName;
    readonly string subscriptionName;
    readonly string inputQueueAddress;
    ServiceBusSessionProcessor? sessionProcessor;
    ServiceBusSender sender;
    CancellationTokenSource forwardingCancellationTokenSource;

    public OrderedSubscriptionForwarder(string topicName, string subscriptionName, string inputQueueAddress)
    {
        this.topicName = topicName;
        this.subscriptionName = subscriptionName;
        this.inputQueueAddress = inputQueueAddress;
        serviceBusClient = new ServiceBusClient(inputQueueAddress, new ServiceBusClientOptions
        {
            EnableCrossEntityTransactions = true // to ensure that completing the incoming message and outgoing send are wrapped in a transaction to avoid duplicates
        }); // TODO: check missing options
    }

    public async Task StartReceive(CancellationToken cancellationToken)
    {
        var sessionReceiveOptions = new ServiceBusSessionProcessorOptions
        {
            PrefetchCount = 50, // TODO: do we want to make the prefetchcount configurable for shoveling
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            Identifier = $"Forwarding-Processor-{topicName}-{subscriptionName}",
            MaxConcurrentSessions = 10,
            AutoCompleteMessages = false,
        };

        sessionProcessor = serviceBusClient.CreateSessionProcessor(inputQueueAddress, sessionReceiveOptions);
        sessionProcessor.ProcessErrorAsync += OnProcessorError;
        sessionProcessor.ProcessMessageAsync += OnProcessMessage;

        forwardingCancellationTokenSource = new CancellationTokenSource();

        await sessionProcessor.StartProcessingAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    async Task OnProcessMessage(ProcessSessionMessageEventArgs arg)
    {
        using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            var serviceBusMessage = new ServiceBusMessage
            {
                Body = arg.Message.Body,
                ContentType = arg.Message.ContentType,
                CorrelationId = arg.Message.CorrelationId,
                MessageId = arg.Message.MessageId,
                PartitionKey = arg.Message.PartitionKey,
                ReplyTo = arg.Message.ReplyTo,
                ReplyToSessionId = arg.Message.ReplyToSessionId,
                ScheduledEnqueueTime = arg.Message.ScheduledEnqueueTime,
                SessionId = arg.Message.SessionId,
                Subject = arg.Message.Subject,
                TimeToLive = arg.Message.TimeToLive,
                To = arg.Message.To
            };

            foreach (var messageApplicationProperty in arg.Message.ApplicationProperties)
            {
                serviceBusMessage.ApplicationProperties.Add(messageApplicationProperty.Key, messageApplicationProperty.Value);
            }

            await arg.CompleteMessageAsync(arg.Message, forwardingCancellationTokenSource.Token).ConfigureAwait(false);
            sender = serviceBusClient.CreateSender(inputQueueAddress, new ServiceBusSenderOptions
            {
                Identifier = $"Forwarding-Sender-{topicName}-{subscriptionName}"
            });
            await sender.SendMessageAsync(serviceBusMessage, forwardingCancellationTokenSource.Token).ConfigureAwait(false);
            ts.Complete();
        }
    }

    Task OnProcessorError(ProcessErrorEventArgs arg)
    {
        return Task.CompletedTask;
    }
}