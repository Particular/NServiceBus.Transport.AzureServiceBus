namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    [TestFixture]
    public class When_MessageLockLostException_is_thrown : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_not_raise_critical_error(TransportTransactionMode transactionMode)
        {
            var criticalErrorInvoked = new TaskCompletionSource<bool>();
            var criticalErrorCalled = false;

            OnTestTimeout(() => criticalErrorInvoked.SetResult(false));

            var firstInvocation = true;

            await StartPump(
                (_, __) =>
                {
                    if (firstInvocation)
                    {
                        firstInvocation = false;
                        throw new ServiceBusException("from onMessage", ServiceBusFailureReason.MessageLockLost);
                    }

                    return Task.CompletedTask;
                },
                (_, __) =>
                {
                    throw new ServiceBusException("from onError", ServiceBusFailureReason.MessageLockLost);
                },
                transactionMode,
                (_, __, ___) =>
                {
                    criticalErrorCalled = true;
                    criticalErrorInvoked.SetResult(true);
                }
            );

            await SendMessage(InputQueueName);

            await criticalErrorInvoked.Task;

            Assert.IsFalse(criticalErrorCalled, $"Should not invoke critical error for {nameof(ServiceBusException)}"); //TODO: check for Reason
            Assert.IsFalse(criticalErrorInvoked.Task.Result);
        }
    }
}