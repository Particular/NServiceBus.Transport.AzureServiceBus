namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving
{
    using System;
    using System.Collections.Generic;
    using AdvancedExtensibility;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.Transport.AzureServiceBus.Configuration;
    using NUnit.Framework;

    [TestFixture]
    public class MessageExtensionsTests
    {
        [Test]
        public void Should_extract_headers()
        {
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId",
                contentType: "SomeContentType",
                properties: new Dictionary<string, object>
                {
                    ["NServiceBus.Transport.Encoding"] = "SomeEncoding",
                    ["Property1"] = "SomeProperty1"
                }, replyTo: "SomeReplyTo", correlationId: "SomeCorrelationId");

            var headers = message.GetNServiceBusHeaders();

            Assert.Multiple(() =>
            {
                Assert.That(headers.ContainsKey(TransportMessageHeaders.TransportEncoding), Is.False);
                Assert.That(headers["Property1"], Is.EqualTo("SomeProperty1"));
                Assert.That(headers[Headers.ReplyToAddress], Is.EqualTo("SomeReplyTo"));
                Assert.That(headers[Headers.CorrelationId], Is.EqualTo("SomeCorrelationId"));
                Assert.That(headers[Headers.ContentType], Is.EqualTo("SomeContentType"));
            });
        }

        [Test]
        public void Should_use_broker_message_id_when_present()
        {
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId");

            var messageId = message.GetMessageId();

            Assert.That(messageId, Is.EqualTo("SomeId"));
        }

        [Test]
        public void Should_use_nservicebus_message_id_header_when_broker_message_id_is_missing()
        {
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: new Dictionary<string, object> { [Headers.MessageId] = "NServiceBusId" });

            var messageId = message.GetMessageId();

            Assert.That(messageId, Is.EqualTo("NServiceBusId"));
        }

        [Test]
        public void Should_use_nservicebus_message_id_header_as_string_when_broker_message_id_is_missing()
        {
            var newGuid = Guid.NewGuid();
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(properties: new Dictionary<string, object> { [Headers.MessageId] = newGuid });

            var messageId = message.GetMessageId();

            Assert.That(messageId, Is.EqualTo(newGuid.ToString()));
        }

        [Test]
        public void Should_derive_message_id_from_enqueued_time_and_sequence_number_if_not_set()
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            const long sequenceNumber = 42;
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(enqueuedTime: enqueuedTime, sequenceNumber: sequenceNumber);

            var messageId = message.GetMessageId();

            Assert.That(messageId, Is.EqualTo(GuidHelper.CreateVersion8(enqueuedTime, sequenceNumber).ToString()));
        }

        [Test]
        public void Should_derive_message_id_from_enqueued_time_and_sequence_number_if_blank_space()
        {
            var enqueuedTime = DateTimeOffset.UtcNow;
            const long sequenceNumber = 42;
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "   ", enqueuedTime: enqueuedTime, sequenceNumber: sequenceNumber);

            var messageId = message.GetMessageId();

            Assert.That(messageId, Is.EqualTo(GuidHelper.CreateVersion8(enqueuedTime, sequenceNumber).ToString()));
        }
    }
}