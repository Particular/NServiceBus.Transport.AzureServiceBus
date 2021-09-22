namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Azure.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageSenderPoolTests
    {
        [Test]
        public async Task Should_get_cached_sender_per_destination()
        {
            var pool = new MessageSenderPool(new ServiceBusClient(connectionString));

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1");
                pool.ReturnMessageSender(firstMessageSenderDest1);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2");
                pool.ReturnMessageSender(firstMessageSenderDest2);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1");
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2");

                Assert.AreSame(firstMessageSenderDest1, secondMessageSenderDest1);
                Assert.AreSame(firstMessageSenderDest2, secondMessageSenderDest2);
                Assert.AreNotSame(firstMessageSenderDest1, firstMessageSenderDest2);
                Assert.AreNotSame(secondMessageSenderDest1, secondMessageSenderDest2);
            }
            finally
            {
                await pool.Close();
            }
        }

        static string connectionString = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake=";
    }
}