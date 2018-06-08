namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
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

            receiver = new MessageReceiver(connectionString, settings.InputQueue, receiveMode, retryPolicy: null, prefetchCount);

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

                    var receiveTask = receiver.ReceiveAsync();

                    ProcessMessage(receiveTask).Ignore();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ProcessMessage(Task<Message> receiveTask)
        {
            var message = await receiveTask.ConfigureAwait(false);

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null)
            {
                return;
            }

            // TODO: wrap in try/catch and dead-letter message if we can't convert/use it
            var messageId = message.GetMessageId();
            var headers = message.GetNServiceBusHeaders();
            var body = message.GetBody();

            var transportTransaction = CreateTransportTransaction(message.PartitionKey);

            using (var receiveCancellationTokenSource = new CancellationTokenSource())
            {
                var messageContext = new MessageContext(messageId, headers, body, transportTransaction, receiveCancellationTokenSource, new ContextBag());

                try
                {
                    var scope = settings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
                        ? new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions
                        {
                            IsolationLevel = IsolationLevel.Serializable
                        }, TransactionScopeAsyncFlowOption.Enabled)
                        : null;

                    using (scope)
                    {
                        await onMessage(messageContext).ConfigureAwait(false);

                        if (receiveCancellationTokenSource.IsCancellationRequested)
                        {
                            // TODO: perform safe abandon
                            await receiver.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // TODO: perform safe completion only when ReceiveMode is PeekLock
                            await receiver.CompleteAsync(message.SystemProperties.LockToken).ConfigureAwait(false);

                            scope?.Complete();
                        }
                    }
                }
                catch (Exception e)
                {
                    // invoke onError
                }
            }
        }

        TransportTransaction CreateTransportTransaction(string incomingQueuePartitionKey)
        {
            var transportTransaction = new TransportTransaction();

            if (settings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive)
            {
                transportTransaction.Set(receiver.ServiceBusConnection);
                transportTransaction.Set("IncomingQueue", settings.InputQueue);
                transportTransaction.Set("IncomingQueue.PartitionKey", incomingQueuePartitionKey);
            }

            return transportTransaction;
        }

        public Task Stop()
        {
            semaphore?.Dispose();
            messageProcessing?.Dispose();

            return Task.CompletedTask;
        }
    }
}