namespace NServiceBus.Transport.AzureServiceBus.Receiving;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Azure.Messaging.ServiceBus;
using Logging;

class OrderedSubscriptionForwarder(ServiceBusClient forwardingClient, string topicName, string subscriptionName, string inputQueueAddress)
    : IAsyncDisposable
{
    static readonly ILog Logger = LogManager.GetLogger<OrderedSubscriptionForwarder>();

    HashSet<string> eventTypes = [];
    ServiceBusSessionProcessor? sessionProcessor;
    ServiceBusSender? sender;
    CancellationTokenSource forwardingCancellationTokenSource = new();

    public async Task Start(CancellationToken cancellationToken = default)
    {
        var sessionReceiveOptions = new ServiceBusSessionProcessorOptions
        {
            PrefetchCount = 50, // TODO: do we want to make the prefetch count configurable
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            Identifier = $"Forwarding-Processor-{topicName}-{subscriptionName}",
            MaxConcurrentSessions = 10, // TODO: do we want to make the session concurrency configurable
            AutoCompleteMessages = false,
        };

        sessionProcessor = forwardingClient.CreateSessionProcessor(topicName, subscriptionName, sessionReceiveOptions);
        sessionProcessor.ProcessErrorAsync += OnError;
        sessionProcessor.ProcessMessageAsync += OnMessage;

        await sessionProcessor.StartProcessingAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
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

#pragma warning disable PS0018
    async Task OnMessage(ProcessSessionMessageEventArgs arg)
#pragma warning restore PS0018
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
        sender = forwardingClient.CreateSender(inputQueueAddress, new ServiceBusSenderOptions
        {
            Identifier = $"Forwarding-Sender-{topicName}-{subscriptionName}"
        });
        await sender.SendMessageAsync(serviceBusMessage, forwardingCancellationTokenSource.Token).ConfigureAwait(false);
        ts.Complete();
    }

#pragma warning disable PS0018
    Task OnError(ProcessErrorEventArgs arg)
#pragma warning restore PS0018
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

    public void RegisterType(string eventTypeFullName)
    {
        eventTypes.Add(eventTypeFullName);
    }

    public bool UnregisterType(string eventTypeFullName)
    {
        eventTypes.Remove(eventTypeFullName);
        return eventTypes.Count == 0;
    }
}