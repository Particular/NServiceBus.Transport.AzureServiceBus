namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System;
    using NServiceBus.Routing;
    using NUnit.Framework;

    [TestFixture]
    public class OutgoingMessageExtensionsTests
    {
        const string TransportEncoding = "NServiceBus.Transport.Encoding";
        const string Dummy = "DUMMY";

        [Test]
        public void Should_not_contain_legacy_header_when_disabled()
        {
            // Arrange
            TransportOperation transportOperation = CreateTransportOperation();
            var transportOperations = new TransportOperations(transportOperation);
            var outgoingMessage = transportOperations.UnicastTransportOperations[0];

            // Act
            var serviceBusMessage = outgoingMessage.ToAzureServiceBusMessage(incomingQueuePartitionKey: Dummy, doNotSendTransportEncodingHeader: true);

            Assert.That(serviceBusMessage.ApplicationProperties.Keys, Has.No.Member(TransportEncoding));
        }


        [Test]
        public void Should_contain_legacy_header_by_default()
        {
            // Arrange
            TransportOperation transportOperation = CreateTransportOperation();
            var transportOperations = new TransportOperations(transportOperation);
            var outgoingMessage = transportOperations.UnicastTransportOperations[0];

            // Act
            var serviceBusMessage = outgoingMessage.ToAzureServiceBusMessage(incomingQueuePartitionKey: Dummy);

            Assert.That(serviceBusMessage.ApplicationProperties.Keys, Has.Member(TransportEncoding));
        }

        static TransportOperation CreateTransportOperation()
        {
            var messageId = Guid.NewGuid().ToString();
            var message = new OutgoingMessage(messageId, [], ReadOnlyMemory<byte>.Empty);
            var transportOperation = new TransportOperation(
                message,
                addressTag: new UnicastAddressTag(Dummy),
                properties: null,
                DispatchConsistency.Default
            );
            return transportOperation;
        }
    }
}