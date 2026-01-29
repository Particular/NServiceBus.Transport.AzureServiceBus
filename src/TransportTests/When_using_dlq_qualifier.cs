namespace NServiceBus.Transport.AzureServiceBus.TransportTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NUnit.Framework;
using Routing;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
[CancelAfter(15000)]
public class When_using_dlq_qualifier
{
    const string TestIdHeaderKey = "TestId";
    string inputQueueName;
    readonly string TestId = Guid.NewGuid().ToString();
    CancellationToken StopToken => TestContext.CurrentContext.CancellationToken;

    [SetUp]
    public void SetUp()
    {
        var test = TestContext.CurrentContext.Test;
        inputQueueName = $"{test.DisplayName}.{test.Name}";
        // Remove empty segments so we don't get leading/trailing underscores when parentheses are at the ends
        inputQueueName = string.Join("_", inputQueueName.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries));
    }

    [Test]
    public async Task Should_receive_from_dlq()
    {
        // Setup
        await SendViaTransportSeam(StopToken);
        await ReceiveAndDeadLetterViaSdk(StopToken);
        // Act
        await BlockUntilReceivedViaTransportSeam(StopToken);
        // Assert
        Assert.Pass("Received message");
    }

    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.None)]
    public async Task Should_work_with_transactionmode(TransportTransactionMode mode)
    {
        // Arrange
        await SendViaTransportSeam(StopToken);
        await ReceiveAndDeadLetterViaSdk(StopToken);
        // Act
        await SeamReceiveFromDlqAndSendTestMessage(mode, StopToken);
        var received = await NativeBlockUntilReceiveMessage(StopToken);
        // Assert
        switch (mode)
        {
            case TransportTransactionMode.SendsAtomicWithReceive:
            case TransportTransactionMode.TransactionScope:
                if (received)
                {
                    Assert.Fail($"Incorrectly received test message");
                }

                break;
            case TransportTransactionMode.None:
            case TransportTransactionMode.ReceiveOnly:
                if (!received)
                {
                    Assert.Fail("Should have received test message");
                }

                break;
            default:
                break;
        }
    }

    async Task<bool> NativeBlockUntilReceiveMessage(CancellationToken cancellationToken)
    {
        string fullyQualifiedNamespace = ConfigureAzureServiceBusTransportInfrastructure.ConnectionString;
        await using ServiceBusClient client = new(fullyQualifiedNamespace);
        var receiver = client.CreateReceiver(inputQueueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        try
        {
            await foreach (var receivedMessage in receiver.ReceiveMessagesAsync(cancellationToken: cancellationToken))
            {
                var testId = receivedMessage.ApplicationProperties[TestIdHeaderKey];
                Console.WriteLine($"Receive test {testId} but waiting for {TestId}");
                if (TestId == (string)testId)
                {
                    return true;
                }
            }

            throw new InvalidOperationException("Unexpected as ReceivedMessagesAsync should throw OperationCanceledException");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    async Task SeamReceiveFromDlqAndSendTestMessage(TransportTransactionMode mode, CancellationToken cancellationToken)
    {
        var c = new ConfigureAzureServiceBusTransportInfrastructure();
        var transportDefinition = (AzureServiceBusTransport)c.CreateTransportDefinition();
        transportDefinition.TransportTransactionMode = mode;

        var hostSettings = new HostSettings(
            inputQueueName,
            string.Empty,
            new StartupDiagnosticEntries(),
            (message, ex, token) => { },
            false
        );

        var deadLetterQueue = new QueueAddress(inputQueueName, qualifier: QueueAddressQualifier.DeadLetterQueue);

        const string receiverKey = "1";

        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            [new ReceiveSettings(receiverKey, deadLetterQueue, true, false, hostSettings.Name + ".error")],
            [],
            cancellationToken
        );


        var waitForDlqMessageToBeReceived = new TaskCompletionSource();
        var dlqReceiver = transportInfrastructure.Receivers[receiverKey];
        await dlqReceiver.Initialize(
            new PushRuntimeSettings(), async (context, _) =>
            {
                var testId = context.Headers[TestIdHeaderKey];
                if (testId == TestId)
                {
                    Console.WriteLine("Send new message");
                    var message = new OutgoingMessage(TestId, new Dictionary<string, string> { [TestIdHeaderKey] = TestId }, null);
                    var operation = new TransportOperation(message, new UnicastAddressTag(inputQueueName));
                    var operations = new TransportOperations(operation);
                    await transportInfrastructure.Dispatcher.Dispatch(operations, context.TransportTransaction, cancellationToken);
                    Console.WriteLine("Signal received");
                    waitForDlqMessageToBeReceived.TrySetResult();
                    throw new Exception("Force the TX to rollback");
                }
            },
            (context, token) => Task.FromResult(ErrorHandleResult.Handled), cancellationToken);

        await dlqReceiver.StartReceive(cancellationToken);

        cancellationToken.Register(() => waitForDlqMessageToBeReceived.TrySetCanceled(cancellationToken));
        Console.WriteLine("Waiting for receive.....");
        await waitForDlqMessageToBeReceived.Task;
        foreach (var r in transportInfrastructure.Receivers.Values)
        {
            await r.StopReceive(cancellationToken);
        }

        await transportInfrastructure.Shutdown(cancellationToken);
        Console.WriteLine("Shutdown completed!");
    }

    async Task SendViaTransportSeam(CancellationToken cancellationToken)
    {
        var c = new ConfigureAzureServiceBusTransportInfrastructure();
        var transportDefinition = (AzureServiceBusTransport)c.CreateTransportDefinition();

        var hostSettings = new HostSettings(
            inputQueueName,
            string.Empty,
            new StartupDiagnosticEntries(),
            (_, _, _) => { },
            true
        );

        var transportInfrastructure = await transportDefinition.Initialize(hostSettings,
            [new ReceiveSettings("1", new QueueAddress(inputQueueName), true, true, hostSettings.Name + ".error")], // Required for the queue to be created
            [],
            cancellationToken
        );

        var message = new OutgoingMessage(TestId, new Dictionary<string, string> { [TestIdHeaderKey] = TestId }, null);
        var operation = new TransportOperation(message, new UnicastAddressTag(inputQueueName));
        var operations = new TransportOperations(operation);
        await transportInfrastructure.Dispatcher.Dispatch(operations, new TransportTransaction(), cancellationToken);
        await transportInfrastructure.Shutdown(cancellationToken);
    }

    async Task ReceiveAndDeadLetterViaSdk(CancellationToken cancellationToken)
    {
        string fullyQualifiedNamespace = ConfigureAzureServiceBusTransportInfrastructure.ConnectionString;
        await using ServiceBusClient client = new(fullyQualifiedNamespace);

        var receiver = client.CreateReceiver(inputQueueName);
        await foreach (var receivedMessage in receiver.ReceiveMessagesAsync(cancellationToken: cancellationToken))
        {
            var testId = receivedMessage.ApplicationProperties[TestIdHeaderKey];
            if (TestId == (string)testId)
            {
                await receiver.DeadLetterMessageAsync(receivedMessage, "unittest", "DLQ message to test receiving from DLQ", cancellationToken);
                return;
            }
        }
    }

    async Task BlockUntilReceivedViaTransportSeam(CancellationToken cancellationToken)
    {
        var c = new ConfigureAzureServiceBusTransportInfrastructure();
        var transportDefinition = (AzureServiceBusTransport)c.CreateTransportDefinition();

        var hostSettings = new HostSettings(
            inputQueueName,
            string.Empty,
            new StartupDiagnosticEntries(),
            (message, ex, token) => { },
            true
        );

        var deadLetterQueue = new QueueAddress(inputQueueName, qualifier: QueueAddressQualifier.DeadLetterQueue);

        const string receiverKey = "1";

        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            [new ReceiveSettings(receiverKey, deadLetterQueue, true, false, hostSettings.Name + ".error")],
            [], cancellationToken);

        var waitForDlqMessageToBeReceived = new TaskCompletionSource();
        var dlqReceiver = transportInfrastructure.Receivers[receiverKey];
        await dlqReceiver.Initialize(
            new PushRuntimeSettings(),
            (context, _) =>
            {
                var testId = context.Headers[TestIdHeaderKey];
                if (testId == TestId)
                {
                    waitForDlqMessageToBeReceived.SetResult();
                }

                return Task.CompletedTask;
            },
            (context, token) => Task.FromResult(ErrorHandleResult.Handled), cancellationToken);

        await dlqReceiver.StartReceive(cancellationToken);

        cancellationToken.Register(() => waitForDlqMessageToBeReceived.SetCanceled(cancellationToken));
        await waitForDlqMessageToBeReceived.Task;
        foreach (var r in transportInfrastructure.Receivers.Values)
        {
            await r.StopReceive(cancellationToken).ConfigureAwait(false);
        }

        await transportInfrastructure.Shutdown(cancellationToken);
    }
}