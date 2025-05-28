namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using BitFaster.Caching;
using Logging;

static class ProcessMessageEventArgsExtensions
{
    public static async ValueTask<bool> TrySafeCompleteMessage(this ProcessMessageEventArgs args,
        ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode,
        ICache<string, bool> messagesToBeCompleted,
        CancellationToken cancellationToken = default)
    {
        if (transportTransactionMode == TransportTransactionMode.ReceiveOnly && messagesToBeCompleted.TryGet(message.GetMessageId(), out _))
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Received message with id '{0}' was marked as successfully completed. Trying to immediately acknowledge the message without invoking the pipeline.", message.GetMessageId());
            }

            try
            {
                await args.CompleteMessageAsync(message, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            // Doing a more generous catch here to make sure we are not losing the ID and can mark it to be completed another time
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                messagesToBeCompleted.AddOrUpdate(message.GetMessageId(), true);
                throw;
            }
        }
        return false;
    }

    public static async ValueTask<bool> TrySafeAbandonMessage(this ProcessMessageEventArgs args,
        ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode,
        CancellationToken cancellationToken = default)
    {
        // TransportTransactionMode.None uses ReceiveAndDelete mode which means the message is already removed from the queue
        // once we get it. Therefore, we don't need to abandon it.
        if (transportTransactionMode != TransportTransactionMode.None && message.LockedUntil < DateTimeOffset.UtcNow)
        {
            Logger.Warn(
                $"Skip handling the message with id '{message.GetMessageId()}' because the lock has expired at '{message.LockedUntil}'. " +
                "This is usually an indication that the endpoint prefetches more messages than it is able to handle within the configured" +
                " peek lock duration. Consider tweaking the prefetch configuration to values that are better aligned with the concurrency" +
                " of the endpoint and the time it takes to handle the messages.");

            try
            {
                await args.SafeAbandonMessage(message, transportTransactionMode, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (Exception e) when (!e.IsCausedBy(cancellationToken))
            {
                if (Logger.IsDebugEnabled)
                {
                    // nothing we can do about it, message will be retried
                    Logger.Debug($"Error abandoning the message with id '{message.GetMessageId()}' because the lock has expired at '{message.LockedUntil}.", e);
                }
            }
        }
        return false;
    }

    public static async Task SafeDeadLetterMessage(this ProcessMessageEventArgs args, ServiceBusReceivedMessage message,
        TransportTransactionMode transportTransactionMode, Exception exception, CancellationToken cancellationToken = default)
    {
        if (transportTransactionMode != TransportTransactionMode.None)
        {
            Logger.Warn($"Poison message detected. Message will be moved to the poison queue. Exception: {exception.Message}", exception);

            try
            {
                await args.DeadLetterMessageAsync(message,
                        deadLetterReason: "Poisoned message",
                        deadLetterErrorDescription: exception.Message,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception deadLetterEx) when (!deadLetterEx.IsCausedBy(cancellationToken))
            {
                if (Logger.IsDebugEnabled)
                {
                    // nothing we can do about it, message will be retried
                    Logger.Debug("Error dead lettering poisoned message.", deadLetterEx);
                }
            }
        }
        else
        {
            Logger.Warn($"Poison message detected. Message will be discarded, transaction mode is set to None. Exception: {exception.Message}", exception);
        }
    }

    public static async Task SafeCompleteMessage(this ProcessMessageEventArgs args,
        ServiceBusReceivedMessage message, TransportTransactionMode transportTransactionMode,
        AzureServiceBusTransportTransaction azureServiceBusTransaction,
        ICache<string, bool> messagesToBeCompleted,
        CancellationToken cancellationToken = default)
    {
        if (transportTransactionMode != TransportTransactionMode.None)
        {
            try
            {
                using var scope = azureServiceBusTransaction.ToTransactionScope();
                await args.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                scope.Complete();
            }
            catch (ServiceBusException e) when (transportTransactionMode == TransportTransactionMode.ReceiveOnly && e.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                // We tried to complete the message because it was successfully either by the pipeline or recoverability, but the lock was lost.
                // To make sure we are not reprocessing it unnecessarily we are tracking the message ID and will complete it
                // on the next receive. For SendsWithAtomicReceive it is necessary to throw which causes the rollback
                // of the transaction and will trigger recoverability.
                messagesToBeCompleted.AddOrUpdate(message.GetMessageId(), true);
            }
        }
    }

    public static async Task SafeAbandonMessage(this ProcessMessageEventArgs args, ServiceBusReceivedMessage message,
        TransportTransactionMode transportTransactionMode, CancellationToken cancellationToken = default)
    {
        if (transportTransactionMode != TransportTransactionMode.None)
        {
            try
            {
                await args.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                if (Logger.IsDebugEnabled)
                {
                    // We tried to abandon the message because it needs to be retried, but the lock was lost.
                    // the message will reappear on the next receive anyway so we can just ignore this case.
                    Logger.DebugFormat("Attempted to abandon the message with id '{0}' but the lock was lost.", message.GetMessageId());
                }
            }
        }
    }

    // The extension methods here are related to functionality of the message pump. Therefore the same logger name
    // is used as the message pump.
    static readonly ILog Logger = LogManager.GetLogger<MessagePump>();
}