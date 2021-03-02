namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
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
                (context, ct) =>
                {
                    if (firstInvocation)
                    {
                        firstInvocation = false;
                        throw new MessageLockLostException("from onMessage");
                    }

                    return Task.CompletedTask;
                },
                (context, ct) =>
                {
                    throw new MessageLockLostException("from onError");
                },
                (context, token) => Task.CompletedTask,
                transactionMode,
                (message, exception, ct) =>
                {
                    criticalErrorCalled = true;
                    criticalErrorInvoked.SetResult(true);
                }
            );

            await SendMessage(InputQueueName);

            await criticalErrorInvoked.Task;

            Assert.IsFalse(criticalErrorCalled, $"Should not invoke critical error for {nameof(MessageLockLostException)}");
            Assert.IsFalse(criticalErrorInvoked.Task.Result);
        }
    }
}