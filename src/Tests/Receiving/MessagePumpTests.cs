namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving
{
    using System;
    using System.Collections.Generic;
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

            var pump = new MessagePump(fakeClient, null, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.CompletedTask,
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.That(fakeReceiver.CompletedMessages, Has.Exactly(1)
                .Matches<ServiceBusReceivedMessage>(message => message.MessageId == "SomeId"));
            Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
        }

        [Test]
        public async Task Should_not_process_message_when_lock_expired()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, null, new AzureServiceBusTransport(), "receiveAddress",
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

            Assert.That(fakeReceiver.CompletedMessages, Is.Empty);
            Assert.That(fakeReceiver.AbandonedMessages, Has.Exactly(1)
                .Matches<(ServiceBusReceivedMessage Message, IDictionary<string, object> Props)>(abandoned => abandoned.Message.MessageId == "SomeId"));
            Assert.That(pumpWasCalled, Is.False);
        }

        [Test]
        public async Task Should_abandon_message_upon_failure_with_retry_required()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, null, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.FromException<InvalidOperationException>(new InvalidOperationException()),
                (context, token) => Task.FromResult(ErrorHandleResult.RetryRequired), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.That(fakeReceiver.AbandonedMessages, Has.Exactly(1)
                .Matches<(ServiceBusReceivedMessage Message, IDictionary<string, object> Props)>(abandoned => abandoned.Message.MessageId == "SomeId"));
            Assert.That(fakeReceiver.CompletedMessages, Is.Empty);
        }

        [Test]
        public async Task Should_complete_message_upon_failure_with_handled()
        {
            var fakeClient = new FakeServiceBusClient();
            var fakeReceiver = new FakeReceiver();

            var pump = new MessagePump(fakeClient, null, new AzureServiceBusTransport(), "receiveAddress",
                new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (s, exception, arg3) => { }, null);

            await pump.Initialize(new PushRuntimeSettings(1), (context, token) => Task.FromException<InvalidOperationException>(new InvalidOperationException()),
                (context, token) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
            await pump.StartReceive();

            var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

            await fakeClient.Processors["receiveAddress"].ProcessMessage(receivedMessage, fakeReceiver);

            Assert.That(fakeReceiver.CompletedMessages, Has.Exactly(1)
                .Matches<ServiceBusReceivedMessage>(message => message.MessageId == "SomeId"));
            Assert.That(fakeReceiver.AbandonedMessages, Is.Empty);
        }
    }
}