namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using NUnit.Framework;

    [TestFixture]
    public class MessageSenderPoolTests
    {
        [Test]
        public void Should_get_cached_sender()
        {
            var pool = new MessageSenderPool("Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake=");

            var firstMessageSender = pool.GetMessageSender("dest");

            pool.ReturnMessageSender(firstMessageSender);

            var secondMessageSender = pool.GetMessageSender("dest");

            Assert.AreEqual(firstMessageSender, secondMessageSender);
        }
    }
}