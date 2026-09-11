namespace NServiceBus.Transport.AzureServiceBus.TransportTests.SessionEnabled;

using System;
using System.Threading.Tasks;
using NServiceBus.TransportTests;
using NUnit.Framework;

[TestFixture]
public class When_sending_to_session_enabled_queue_without_session_id : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_include_destination_in_exception_message(TransportTransactionMode transactionMode)
    {
        await Initialize(
            (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled),
            transactionMode,
            cancellationToken: TestTimeoutCancellationToken);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SendMessage(
                InputQueueName,
                dispatchProperties: [],
                cancellationToken: TestTimeoutCancellationToken));

        Assert.That(exception.Message, Does.Contain(
            $"The SessionId was not set on a message, and it cannot be sent to the entity {InputQueueName} that has sessions enabled"));
    }
}