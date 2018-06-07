namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;

    class MessagePump : IPushMessages
    {
        readonly string connectionString;
        readonly TransportType transportType;
        readonly int prefetchMultiplier;
        readonly int overriddenPrefetchCount;

        // Init
        Func<MessageContext, Task> onMessage;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        CriticalError criticalError;
        PushSettings settings;
        MessageReceiver receiver;
        
        // Start
        SemaphoreSlim semaphore;
        CancellationTokenSource messageProcessing;

        public MessagePump(string connectionString, TransportType transportType, int prefetchMultiplier, int overriddenPrefetchCount)
        {
            this.connectionString = connectionString;
            this.transportType = transportType;
            this.prefetchMultiplier = prefetchMultiplier;
            this.overriddenPrefetchCount = overriddenPrefetchCount;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
            this.settings = settings;

            // TODO: hook up critical error

            // TODO: calculate prefetch count
            var prefetchCount = overriddenPrefetchCount;

            var receiveMode = settings.RequiredTransactionMode == TransportTransactionMode.None ? ReceiveMode.ReceiveAndDelete : ReceiveMode.PeekLock;
            
            receiver = new MessageReceiver(connectionString, settings.InputQueue, receiveMode, retryPolicy:null, prefetchCount);

            return Task.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            semaphore = new SemaphoreSlim(limitations.MaxConcurrency, limitations.MaxConcurrency);

            messageProcessing = new CancellationTokenSource();

            ReceiveLoop().Ignore();
        }

        async Task ReceiveLoop()
        {
            try
            {
                while (!messageProcessing.IsCancellationRequested)
                {
                    await semaphore.WaitAsync(messageProcessing.Token).ConfigureAwait(false);

                    // By default, ASB client long polls for a minute
                    var receiveTask = receiver.ReceiveAsync();

                    ProcessMessage(receiveTask).Ignore();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async  Task ProcessMessage(Task<Message> receiveTask)
        {
            var message = await receiveTask.ConfigureAwait(false);

            // TODO: get message headers
            // TODO: message id can be null, try to get out of message.messageId or NSB headers. Otherwise throw
            // TODO: get body with interop in mind
            var transportTransaction = new TransportTransaction();

            using (var receiveCancellationTokenSource = new CancellationTokenSource())
            {
                var messageContext = new MessageContext(message.MessageId, new Dictionary<string, string>(), new byte[] {0}, transportTransaction, receiveCancellationTokenSource, new ContextBag());

                await onMessage(messageContext).ConfigureAwait(false);
            }
        }

        public Task Stop()
        {
            semaphore?.Dispose();
            messageProcessing?.Dispose();

            return Task.CompletedTask;
        }
    }
}