namespace NServiceBus.Transport.AzureServiceBus.TransportTests;

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.TransportTests;
using NUnit.Framework;

[TestFixture]
public class When_requesting_deadletter_from_recoverability : NServiceBusTransportTest
{
    const string TestIdHeaderName = "TransportTest.TestId";

    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_move_message_to_deadletter_queue(TransportTransactionMode transactionMode)
    {
        var onErrorCalled = CreateTaskCompletionSource<bool>();

        await StartPump(
            (_, _) => throw new Exception("from onMessage"),
            (context, _) =>
            {
                SetDeadLetterRequest(context.TransportTransaction, "Requested by test", "Dead lettered from recoverability");
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
            Assert.That(receivedMessage.ApplicationProperties[TestIdHeaderName], Is.EqualTo(CurrentTestId));
        });
    }

    async Task<ServiceBusReceivedMessage> ReceiveDeadLetteredMessage(CancellationToken cancellationToken)
    {
        await using var client = new ServiceBusClient(ConfigureAzureServiceBusTransportInfrastructure.ConnectionString);
        var receiver = client.CreateReceiver(InputQueueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });

        await foreach (var receivedMessage in receiver.ReceiveMessagesAsync(cancellationToken: cancellationToken))
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

    static void SetDeadLetterRequest(TransportTransaction transportTransaction, string deadLetterReason, string deadLetterErrorDescription)
    {
        var requestType = typeof(AzureServiceBusTransport).Assembly.GetType("NServiceBus.Transport.AzureServiceBus.DeadLetterRequest", throwOnError: true)!;
        var constructor = requestType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(ctor =>
            {
                var parameters = ctor.GetParameters();
                return parameters.Length == 3 &&
                       parameters[0].ParameterType == typeof(string) &&
                       parameters[1].ParameterType == typeof(string);
            });
        var deadLetterRequest = constructor.Invoke([deadLetterReason, deadLetterErrorDescription, null]);
        var setMethod = typeof(TransportTransaction).GetMethods()
            .Single(method => method.Name == nameof(TransportTransaction.Set) && method.IsGenericMethod && method.GetParameters().Length == 1);

        setMethod.MakeGenericMethod(requestType).Invoke(transportTransaction, [deadLetterRequest]);
    }
}
