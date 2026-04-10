namespace NServiceBus.Transport.AzureServiceBus.TransportTests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.TransportTests;
using NUnit.Framework;

[TestFixture]
public class When_requesting_message_to_be_dead_lettered : NServiceBusTransportTest
{
    const string TestIdHeaderName = "TransportTest.TestId";

    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_move_message_to_dead_letter_queue(TransportTransactionMode transactionMode)
    {
        var onErrorCalled = CreateTaskCompletionSource<bool>();

        await StartPump(
            (_, _) => throw new Exception("from onMessage"),
            (context, _) =>
            {
                context.TransportTransaction.Set(new DeadLetterRequest("Requested by test", "Dead lettered from recoverability", new Dictionary<string, object> { { "updated-property", "some-value" } }));
                onErrorCalled.TrySetResult(true);
                return Task.FromResult(ErrorHandleResult.Handled);
            },
            transactionMode,
            cancellationToken: TestTimeoutCancellationToken);

        await SendMessage(InputQueueName, cancellationToken: TestTimeoutCancellationToken);
        await onErrorCalled.Task;
        await StopPump(TestTimeoutCancellationToken);

        var receivedMessage = await ReceiveDeadLetteredMessage(TestTimeoutCancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(receivedMessage, Is.Not.Null);
            Assert.That(receivedMessage!.DeadLetterReason, Is.EqualTo("Requested by test"));
            Assert.That(receivedMessage.DeadLetterErrorDescription, Is.EqualTo("Dead lettered from recoverability"));
            Assert.That(receivedMessage.ApplicationProperties.ContainsKey("updated-property"), Is.True);
        });
    }

    async Task<ServiceBusReceivedMessage> ReceiveDeadLetteredMessage(CancellationToken cancellationToken)
    {
        await using var client = new ServiceBusClient(ConfigureAzureServiceBusTransportInfrastructure.ConnectionString);
        await using var dlqReceiver = client.CreateReceiver(InputQueueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });

        await foreach (var receivedMessage in dlqReceiver.ReceiveMessagesAsync(cancellationToken: cancellationToken))
        {
            if (receivedMessage.ApplicationProperties.TryGetValue(TestIdHeaderName, out var testId) && Equals(testId, CurrentTestId))
            {
                return receivedMessage;
            }
        }

        throw new InvalidOperationException("Unexpected as ReceiveMessagesAsync should throw OperationCanceledException");
    }

    string CurrentTestId => (string)typeof(NServiceBusTransportTest)
        .GetField("testId", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(this)!;
}