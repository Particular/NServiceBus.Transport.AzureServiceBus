namespace NServiceBus.Transport.AzureServiceBus.Receiving;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Logging;

class OrderedSubscriptionForwarder : IAsyncDisposable
{
    static readonly ILog Logger = LogManager.GetLogger<OrderedSubscriptionForwarder>();

    readonly ServiceBusClient serviceBusClient;
    readonly string topicName;
    readonly string subscriptionName;
    readonly string inputQueueAddress;
    ServiceBusSessionProcessor? sessionProcessor;
    ServiceBusSender sender;
    CancellationTokenSource forwardingCancellationTokenSource;

    public OrderedSubscriptionForwarder(ServiceBusClient forwardingClient, string topicName, string subscriptionName, string inputQueueAddress)
    {
        this.topicName = topicName;
        this.subscriptionName = subscriptionName;
        this.inputQueueAddress = inputQueueAddress;
        serviceBusClient = forwardingClient;
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        var sessionReceiveOptions = new ServiceBusSessionProcessorOptions
        {
            PrefetchCount = 50, // TODO: do we want to make the prefetch count configurable
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            Identifier = $"Forwarding-Processor-{topicName}-{subscriptionName}",
            MaxConcurrentSessions = 10, // TODO: do we want to make the session concurrency configurable
            AutoCompleteMessages = false,
        };

        sessionProcessor = serviceBusClient.CreateSessionProcessor(inputQueueAddress, sessionReceiveOptions);
        sessionProcessor.ProcessErrorAsync += OnError;
        sessionProcessor.ProcessMessageAsync += OnMessage;

        forwardingCancellationTokenSource = new CancellationTokenSource();

        await sessionProcessor.StartProcessingAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        await sessionProcessor!.StopProcessingAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await sessionProcessor.CloseAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Operation canceled while stopping the forwarder {sessionProcessor.EntityPath}.", ex);
            }
        }
    }

    async Task OnMessage(ProcessSessionMessageEventArgs arg)
    {
        using var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var serviceBusMessage = new ServiceBusMessage()
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

    Task OnError(ProcessErrorEventArgs arg)
    {
        //TODO: How do we handle forwarding errors?
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (sessionProcessor != null)
        {
            sessionProcessor.ProcessErrorAsync -= OnError;
            sessionProcessor.ProcessMessageAsync -= OnMessage;

            await sessionProcessor.DisposeAsync().ConfigureAwait(false);
            sessionProcessor = null;
        }
    }
}