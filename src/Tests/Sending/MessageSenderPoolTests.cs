namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using Microsoft.Azure.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageSenderPoolTests
    {
        [Test]
        public void Should_get_cached_sender()
        {
            var pool = new MessageSenderPool(new ServiceBusConnectionStringBuilder("Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake="), null);

            var firstMessageSender = pool.GetMessageSender("dest", (null, null));

            pool.ReturnMessageSender(firstMessageSender);

            var secondMessageSender = pool.GetMessageSender("dest", (null, null));

            Assert.AreEqual(firstMessageSender, secondMessageSender);
        }
    }
}