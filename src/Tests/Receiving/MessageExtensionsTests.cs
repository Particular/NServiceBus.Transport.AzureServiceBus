namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving
{
    using System;
    using Configuration;
    using Microsoft.Azure.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageExtensionsTests
    {
        [Test]
        public void Should_process_message_without_body()
        {
            var message = new Message();

            var body = message.GetBody();
            Assert.AreEqual(Array.Empty<byte>(), body);
        }

        [Test]
        public void Should_process_byte_array_message_without_body()
        {
            var message = new Message();
            message.UserProperties[TransportMessageHeaders.TransportEncoding] = "wcf/byte-array";

            var body = message.GetBody();
            Assert.AreEqual(Array.Empty<byte>(), body);
        }
    }
}
