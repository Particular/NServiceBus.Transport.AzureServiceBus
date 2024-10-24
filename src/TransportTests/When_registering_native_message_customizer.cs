namespace NServiceBus.Transport.AzureServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.TransportTests;
    using NUnit.Framework;

    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class When_registering_native_message_customizer : AzureServiceBusTransportTest
    {
        const string ContentTypeTestValue = "text/plain";

        protected override void CustomizeTransport(TransportDefinition transport)
        {
            var asb = (AzureServiceBusTransport)transport;
            asb.NativeMessageCustomization = NativeMessageCustomizerCallback;
        }

        TaskCompletionSource<bool> customizerCalled;

        void NativeMessageCustomizerCallback(IOutgoingTransportOperation message, ServiceBusMessage nativeMessage)
        {
            nativeMessage.ContentType = ContentTypeTestValue;
            customizerCalled.TrySetResult(true);
        }

        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_invoke_the_customizer(TransportTransactionMode transactionMode)
        {
            customizerCalled = CreateTaskCompletionSource<bool>();
            var onErrorCalled = CreateTaskCompletionSource<bool>();
            var onMessageCalled = CreateTaskCompletionSource<bool>();
            string criticalErrorDetails = null;
            ServiceBusReceivedMessage nativeReceivedMessage = null;

            await StartPump(
                (ctx, __) =>
                {
                    nativeReceivedMessage = ctx.Extensions.Get<ServiceBusReceivedMessage>();
                    onMessageCalled.TrySetResult(true);
                    return Task.FromResult(true);
                },
                (_, __) =>
                {
                    onErrorCalled.TrySetResult(true);
                    throw new InvalidOperationException();
                },
                transactionMode,
                (msg, ex, ___) =>
                {
                    criticalErrorDetails = $"{msg}, Exception: {ex}";
                }
            );

            // Act
            await SendMessage(InputQueueName);
            await customizerCalled.Task;
            await onMessageCalled.Task;
            await StopPump();

            Assert.That(nativeReceivedMessage.ContentType, Is.EqualTo(ContentTypeTestValue));
            Assert.That(criticalErrorDetails, Is.Null, $"Should not invoke critical error for {nameof(ServiceBusException)}");
        }
    }
}