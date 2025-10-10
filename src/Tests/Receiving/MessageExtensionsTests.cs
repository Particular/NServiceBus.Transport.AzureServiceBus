namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving
{
    using System.Collections.Generic;
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
    }
}