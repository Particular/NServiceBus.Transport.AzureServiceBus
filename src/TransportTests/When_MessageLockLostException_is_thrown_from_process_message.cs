﻿namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    [TestFixture]
    public class When_MessageLockLostException_is_thrown_from_process_message : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_not_throw_exception_from_on_error(TransportTransactionMode transactionMode)
        {
            bool onErrorCalled = false;
            var onMessageCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;


            await StartPump(
                (_, __) =>
                {
                    onMessageCalled.TrySetResult(true);
                    throw new ServiceBusException("from onMessage", ServiceBusFailureReason.MessageLockLost);
                },
                (_, __) =>
                {
                    onErrorCalled = true;
                    throw new ServiceBusException("from onError", ServiceBusFailureReason.MessageLockLost);
                },
                transactionMode
            );

            await SendMessage(InputQueueName);

            var onMessageResult = await onMessageCalled.Task.ConfigureAwait(false);

            await StopPump();

            Assert.IsTrue(onMessageResult, "The message handler should have been called.");
            Assert.IsFalse(onErrorCalled, "onError should not have been called when a MessageLostLock exception is thrown from onMessage when the transport is in receiveOnly mode.");
            Assert.IsNull(criticalErrorDetails, $"Should not invoke critical error for {nameof(ServiceBusException)}");
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_throw_exception_from_on_error(TransportTransactionMode transactionMode)
        {
            bool onErrorCalled = false;
            var onMessageCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;


            await StartPump(
                (_, __) =>
                {
                    onMessageCalled.TrySetResult(true);
                    throw new ServiceBusException("from onMessage", ServiceBusFailureReason.MessageLockLost);
                },
                (_, __) =>
                {
                    onErrorCalled = true;
                    throw new ServiceBusException("from onError", ServiceBusFailureReason.MessageLockLost);
                },
                transactionMode
            );

            await SendMessage(InputQueueName);

            var onMessageResult = await onMessageCalled.Task.ConfigureAwait(false);

            await StopPump();

            Assert.IsTrue(onMessageResult, "The message handler should have been called.");
            Assert.IsTrue(onErrorCalled, "onError should have been called when a MessageLostLock exception is thrown from onMessage when the transport is not receiveOnly mode.");
            Assert.IsNull(criticalErrorDetails, $"Should not invoke critical error for {nameof(ServiceBusException)}");
        }
    }
}