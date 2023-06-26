namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageSenderRegistryTests
    {
        [Test]
        public async Task Should_get_cached_sender_per_destination()
        {
            var pool = new MessageSenderRegistry(connectionString, null, null, ServiceBusTransportType.AmqpTcp, TransportTransactionMode.None);

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1", null);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2", null);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1", null);
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2", null);

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