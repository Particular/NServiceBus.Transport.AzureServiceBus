namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    [TestFixture]
    public class When_SessionLockLostException_is_thrown : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_not_raise_critical_error_when_thrown_from_on_error(TransportTransactionMode transactionMode)
        {
            var onErrorCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;

            await StartPump(
                (_, __) =>
                {
                    throw new Exception("from onMessage");
                },
                (_, __) =>
                {
                    onErrorCalled.TrySetResult(true);
                    throw new ServiceBusException("from onError", ServiceBusFailureReason.SessionLockLost);
                },
                transactionMode,
                (msg, ex, ___) =>
                {
                    criticalErrorDetails = $"{msg}, Exception: {ex}";
                }
            );

            await SendMessage(InputQueueName);

            await onErrorCalled.Task;

            await StopPump();

            Assert.That(criticalErrorDetails, Is.Null, $"Should not invoke critical error for {nameof(ServiceBusException)}");
        }

        // Only ReceiveOnly is exercised here because that's the only mode where the pump swallows a lock lost
        // exception while completing (see ProcessSessionMessageEventArgsExtensions.SafeCompleteMessage). For
        // SendsAtomicWithReceive the exception is deliberately allowed to propagate and trigger recoverability instead.
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_not_raise_critical_error_when_thrown_while_completing_the_message(TransportTransactionMode transactionMode)
        {
            var onMessageCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;

            await StartPump(
                async (context, cancellationToken) =>
                {
                    // Deliberately abandoning the message ourselves so that when the pump tries to complete it
                    // after successful processing, Azure Service Bus rejects it because the session lock is gone.
                    var args = context.Extensions.Get<ProcessSessionMessageEventArgs>();
                    var message = context.Extensions.Get<ServiceBusReceivedMessage>();
                    await args.AbandonMessageAsync(message, cancellationToken: cancellationToken);

                    onMessageCalled.TrySetResult(true);
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                transactionMode,
                (msg, ex, ___) =>
                {
                    criticalErrorDetails = $"{msg}, Exception: {ex}";
                }
            );

            await SendMessage(InputQueueName);

            await onMessageCalled.Task;

            // Waits for the pump to fully drain the in-flight message, including the SafeCompleteMessage
            // call that runs after onMessage returns, so the assertion below isn't racing that call.
            await StopPump();

            Assert.That(criticalErrorDetails, Is.Null, "Losing the session lock while completing a message should not raise a critical error");
        }

        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_not_raise_critical_error_when_thrown_while_abandoning_the_message(TransportTransactionMode transactionMode)
        {
            var onErrorCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;

            await StartPump(
                async (context, cancellationToken) =>
                {
                    // Deliberately abandoning the message ourselves so that when the pump retries the abandon as
                    // part of recoverability, Azure Service Bus rejects it because the session lock is gone.
                    var args = context.Extensions.Get<ProcessSessionMessageEventArgs>();
                    var message = context.Extensions.Get<ServiceBusReceivedMessage>();
                    await args.AbandonMessageAsync(message, cancellationToken: cancellationToken);

                    throw new Exception("Simulated failure after losing the lock");
                },
                (_, __) =>
                {
                    onErrorCalled.TrySetResult(true);
                    return Task.FromResult(ErrorHandleResult.RetryRequired);
                },
                transactionMode,
                (msg, ex, ___) =>
                {
                    criticalErrorDetails = $"{msg}, Exception: {ex}";
                }
            );

            await SendMessage(InputQueueName);

            await onErrorCalled.Task;

            // Waits for the pump to fully drain the in-flight message, including the SafeAbandonMessage
            // call that runs after onError returns RetryRequired, so the assertion below isn't racing that call.
            await StopPump();

            Assert.That(criticalErrorDetails, Is.Null, "Losing the session lock while abandoning a message should not raise a critical error");
        }
    }
}
