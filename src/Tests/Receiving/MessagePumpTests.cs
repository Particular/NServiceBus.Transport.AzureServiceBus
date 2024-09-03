namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessagePumpTests
    {
        [Test]
        public async Task Should_complete_message_upon_success()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.CompletedTask,
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.Multiple(() =>
            {
                Assert.That(fakeReceiver.CompletedMessages, Has.Exactly(1)
                            .Matches<ServiceBusReceivedMessage>(message => message.MessageId == "SomeId"));
                Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
            });
        }

        [Test]
        public async Task Should_not_process_message_when_lock_expired()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            bool pumpWasCalled = false;

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) =>
                {
                    pumpWasCalled = true;
                    return Task.CompletedTask;
                },
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
            await pump.StartReceive();

            var messageWithLostLock = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(-30));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(messageWithLostLock, fakeReceiver);

            Assert.Multiple(() =>
            {
                Assert.That(fakeReceiver.CompletedMessages, Is.Empty);
                Assert.That(fakeReceiver.AbandonedMessages, Has.Exactly(1)
                    .Matches<(ServiceBusReceivedMessage Message, IDictionary<string, object> Props)>(abandoned => abandoned.Message.MessageId == "SomeId"));
                Assert.That(pumpWasCalled, Is.False);
            });
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_receiveonly_mode_when_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();
            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport { TransportTransactionMode = TransportTransactionMode.ReceiveOnly }, "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pumpExecutingTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var _ = cancellationTokenSource.Token.Register(() => pumpExecutingTaskCompletionSource.TrySetCanceled());

            await pump.Initialize(new PushRuntimeSettings(1), (_, _) =>
                {
                    onMessageCalled++;
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    onErrorCalled++;
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, CancellationToken.None);
            await pump.StartReceive();

            var firstReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));
            var secondReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            fakeReceiver.CompleteMessageCallback = (message, _) => message == firstReceivedMessage ?
                Task.FromException(new ServiceBusException("Lock Lost", reason: ServiceBusFailureReason.MessageLockLost)) :
                Task.CompletedTask;

            var fakeProcessor = fakeClient.Processors["receiveAddress"];
            await fakeProcessor.ProcessMessage(firstReceivedMessage, fakeReceiver);
            await fakeProcessor.ProcessMessage(secondReceivedMessage, fakeReceiver);

            Assert.That(fakeReceiver.CompletedMessages, Does.Not.Contain(firstReceivedMessage));
            Assert.That(fakeReceiver.CompletedMessages, Does.Contain(secondReceivedMessage));
            Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
            Assert.That(onMessageCalled, Is.EqualTo(1));
            Assert.That(onErrorCalled, Is.Zero);
        }

        [Test]
        public async Task Should_abandon_message_in_atomic_mode_when_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();
            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport { TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive }, "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pumpExecutingTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var _ = cancellationTokenSource.Token.Register(() => pumpExecutingTaskCompletionSource.TrySetCanceled());

            await pump.Initialize(new PushRuntimeSettings(1), (_, _) =>
                {
                    onMessageCalled++;
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    onErrorCalled++;
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            fakeReceiver.CompleteMessageCallback = (message, _) => message == receivedMessage ?
                Task.FromException(new ServiceBusException("Lock Lost", reason: ServiceBusFailureReason.MessageLockLost)) :
                Task.CompletedTask;

            var fakeProcessor = fakeClient.Processors["receiveAddress"];
            await fakeProcessor.ProcessMessage(receivedMessage, fakeReceiver);

            Assert.Multiple(() =>
            {
                Assert.That(fakeReceiver.AbandonedMessages.Select((tuple, _) => { var (message, _) = tuple; return message; })
                    .ToList(), Does.Contain(receivedMessage));
                Assert.That(fakeReceiver.CompletedMessages, Is.Empty);
                Assert.That(onMessageCalled, Is.EqualTo(1));
                Assert.That(onErrorCalled, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_receiveonly_mode_when_error_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();
            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport { TransportTransactionMode = TransportTransactionMode.ReceiveOnly }, "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pumpExecutingTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var _ = cancellationTokenSource.Token.Register(() => pumpExecutingTaskCompletionSource.TrySetCanceled());

            await pump.Initialize(new PushRuntimeSettings(1), (_, _) =>
                {
                    onMessageCalled++;
                    return Task.FromException<InvalidOperationException>(new InvalidOperationException());
                },
                (_, _) =>
                {
                    onErrorCalled++;
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, CancellationToken.None);
            await pump.StartReceive();

            var firstReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));
            var secondReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            fakeReceiver.CompleteMessageCallback = (message, _) => message == firstReceivedMessage ?
                Task.FromException(new ServiceBusException("Lock Lost", reason: ServiceBusFailureReason.MessageLockLost)) :
                Task.CompletedTask;

            var fakeProcessor = fakeClient.Processors["receiveAddress"];
            await fakeProcessor.ProcessMessage(firstReceivedMessage, fakeReceiver);
            await fakeProcessor.ProcessMessage(secondReceivedMessage, fakeReceiver);

            Assert.That(fakeReceiver.CompletedMessages, Does.Not.Contain(firstReceivedMessage));
            Assert.That(fakeReceiver.CompletedMessages, Does.Contain(secondReceivedMessage));
            Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
            Assert.That(onMessageCalled, Is.EqualTo(1));
            Assert.That(onErrorCalled, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_abandon_message_upon_failure_with_retry_required()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.FromException<InvalidOperationException>(new InvalidOperationException()),
                (context, token) => Task.FromResult(ErrorHandleResult.RetryRequired), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.Multiple(() =>
            {
                Assert.That(fakeReceiver.AbandonedMessages, Has.Exactly(1)
                            .Matches<(ServiceBusReceivedMessage Message, IDictionary<string, object> Props)>(abandoned => abandoned.Message.MessageId == "SomeId"));
                Assert.That(fakeReceiver.CompletedMessages, Is.Empty);
            });
        }

        [Test]
        public async Task Should_complete_message_upon_failure_with_handled()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.FromException<InvalidOperationException>(new InvalidOperationException()),
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.Multiple(() =>
            {
                Assert.That(fakeReceiver.CompletedMessages, Has.Exactly(1)
                            .Matches<ServiceBusReceivedMessage>(message => message.MessageId == "SomeId"));
                Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
            });
        }
    }
}