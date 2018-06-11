namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Logging;
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
        int maxConcurrency;
        
        static readonly ILog logger = LogManager.GetLogger<MessagePump>();

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
            maxConcurrency = limitations.MaxConcurrency;
            semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

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

                    ProcessMessage(receiveTask)
                        .ContinueWith(_ => semaphore.Release()).Ignore();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ProcessMessage(Task<Message> receiveTask)
        {
            Message message = null;

            try
            {
                message = await receiveTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // TODO: invoke critical error if failing to receive
            }

            // By default, ASB client long polls for a minute and returns null if it times out
            if (message == null)
            {
                return;
            }

            var lockToken = message.SystemProperties.LockToken;

            string messageId;
            Dictionary<string, string> headers;
            byte[] body;

            try
            {
                messageId = message.GetMessageId();
                headers = message.GetNServiceBusHeaders();
                body = message.GetBody();
            }
            catch (Exception exception)
            {
                // TODO: dead-lettering could throw
                await receiver.DeadLetterAsync(lockToken, deadLetterReason: "Poisoned message", deadLetterErrorDescription: exception.Message).ConfigureAwait(false);

                return;
            }

            try
            {
                using (var receiveCancellationTokenSource = new CancellationTokenSource())
                {
                    var transportTransaction = CreateTransportTransaction(message.PartitionKey);

                    var messageContext = new MessageContext(messageId, headers, body, transportTransaction, receiveCancellationTokenSource, new ContextBag());

                    using (var scope = CreateTransactionScope())
                    {
                        await onMessage(messageContext).ConfigureAwait(false);

                        if (receiveCancellationTokenSource.IsCancellationRequested == false)
                        {
                            await receiver.SafeCompleteAsync(settings.RequiredTransactionMode, lockToken).ConfigureAwait(false);

                            scope?.Complete();
                        }
                    }

                    if (receiveCancellationTokenSource.IsCancellationRequested)
                    {
                        await receiver.SafeAbandonAsync(settings.RequiredTransactionMode, lockToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                try
                {
                    ErrorHandleResult result;

                    using (var scope = CreateTransactionScope())
                    {
                        var transportTransaction = CreateTransportTransaction(message.PartitionKey);

                        var errorContext = new ErrorContext(exception, message.GetNServiceBusHeaders(), messageId, body, transportTransaction, message.SystemProperties.DeliveryCount);

                        result = await onError(errorContext).ConfigureAwait(false);

                        if (result == ErrorHandleResult.Handled)
                        {
                            await receiver.SafeCompleteAsync(settings.RequiredTransactionMode, lockToken).ConfigureAwait(false);
                        }

                        scope?.Complete();
                    }

                    if (result == ErrorHandleResult.RetryRequired)
                    {
                        await receiver.SafeAbandonAsync(settings.RequiredTransactionMode, lockToken).ConfigureAwait(false);
                    }
                }
                catch (Exception onErrorException)
                {
                    logger.WarnFormat("Recoverability failed for message with ID {0}. The message will be retried. Exception details: {1}", messageId, onErrorException);
                }
            }
        }

        TransactionScope CreateTransactionScope()
        {
            return settings.RequiredTransactionMode == TransportTransactionMode.SendsAtomicWithReceive
                ? new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable,
                    Timeout = TransactionManager.MaximumTimeout
                }, TransactionScopeAsyncFlowOption.Enabled)
                : null;
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

        public async Task Stop()
        {
            messageProcessing.Cancel();

            while (semaphore.CurrentCount != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            await receiver.CloseAsync().ConfigureAwait(false);

            semaphore?.Dispose();
            messageProcessing?.Dispose();
        }
    }
}