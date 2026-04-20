namespace NServiceBus.Transport.AzureServiceBus.TransportTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.TransportTests;
using NUnit.Framework;

[TestFixture]
public class When_auto_forwarding_dead_lettered_messages : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_forward_dead_lettered_messages_to_the_error_queue(TransportTransactionMode transactionMode)
    {
        CustomizeTransportDefinition = transport => ((AzureServiceBusTransport)transport).AutoForwardDeadLetteredMessagesToErrorQueue = true;

        var onErrorCalled = CreateTaskCompletionSource();

        await StartPump(
            (_, _) => throw new Exception("from onMessage"),
            (context, _) =>
            {
                context.TransportTransaction.Set(new DeadLetterRequest("Some reason", "Some description", new Dictionary<string, object> { { "some-property", "some value" } }));

                onErrorCalled.SetResult();
                return Task.FromResult(ErrorHandleResult.Handled);
            },
            transactionMode);

        await using var client = new ServiceBusClient(ConfigureAzureServiceBusTransportInfrastructure.ConnectionString);

        await SendMessage(InputQueueName);

        await onErrorCalled.Task;

        await using var errorReceiver = client.CreateReceiver(ErrorQueueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var forwardedMessage = await errorReceiver.ReceiveMessageAsync(TestTimeout, TestTimeoutCancellationToken);

        await StopPump();

        Assert.Multiple(() =>
        {
            Assert.That(forwardedMessage.DeadLetterReason, Is.EqualTo("Some reason"));
            Assert.That(forwardedMessage.DeadLetterErrorDescription, Is.EqualTo("Some description"));
            Assert.That(forwardedMessage.ApplicationProperties["some-property"], Is.EqualTo("some value"));
        });
    }
}