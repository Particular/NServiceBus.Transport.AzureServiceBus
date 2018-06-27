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

            var firstMessageSender = pool.GetMessageSender("dest", null, null);

            pool.ReturnMessageSender(firstMessageSender, null);

            var secondMessageSender = pool.GetMessageSender("dest", null, null);

            Assert.AreEqual(firstMessageSender, secondMessageSender);
        }
    }
}