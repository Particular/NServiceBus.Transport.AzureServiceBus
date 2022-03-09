﻿namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    [TestFixture]
    public class When_MessageLockLostException_is_thrown_from_on_error : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_not_raise_critical_error(TransportTransactionMode transactionMode)
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
                    throw new ServiceBusException("from onError", ServiceBusFailureReason.MessageLockLost);
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

            Assert.IsNull(criticalErrorDetails, $"Should not invoke critical error for {nameof(ServiceBusException)}");
        }
    }
}