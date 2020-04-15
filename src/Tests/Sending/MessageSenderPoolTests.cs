namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageSenderPoolTests
    {
        [Test]
        public async Task Should_get_cached_sender_per_destination()
        {
            var pool = new MessageSenderPool(new ServiceBusConnectionStringBuilder(connectionString), null, null);

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1", (null, null));
                pool.ReturnMessageSender(firstMessageSenderDest1);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2", (null, null));
                pool.ReturnMessageSender(firstMessageSenderDest2);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1", (null, null));
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2", (null, null));

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

        [Test]
        public async Task Should_return_correctly_configured_sender()
        {
            var pool = new MessageSenderPool(new ServiceBusConnectionStringBuilder(connectionString), null, null);
            var connection = new ServiceBusConnection(connectionString);

            try
            {
                // unfortunately it is not possible to assert the token provider
                var nonSendViaSender = pool.GetMessageSender("dest1", (null, null));
                var sendViaSender = pool.GetMessageSender("dest2", (connection, "via"));

                Assert.AreEqual("dest1", nonSendViaSender.Path);
                Assert.IsNull(nonSendViaSender.TransferDestinationPath);
                Assert.IsNull(nonSendViaSender.ViaEntityPath);
                Assert.IsTrue(nonSendViaSender.OwnsConnection);

                Assert.AreEqual("via", sendViaSender.ViaEntityPath);
                Assert.AreEqual("via", sendViaSender.Path);
                Assert.AreEqual("dest2", sendViaSender.TransferDestinationPath);
                Assert.IsFalse(sendViaSender.OwnsConnection);
            }
            finally
            {
                await pool.Close();
                await connection.CloseAsync();
            }
        }

        [Test]
        public async Task Should_get_cached_sender_per_destination_for_send_via()
        {
            var pool = new MessageSenderPool(new ServiceBusConnectionStringBuilder(connectionString), null, null);
            var connection = new ServiceBusConnection(connectionString);

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1", (connection, "via"));
                pool.ReturnMessageSender(firstMessageSenderDest1);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2", (connection, "via"));
                pool.ReturnMessageSender(firstMessageSenderDest2);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1", (connection, "via"));
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2", (connection, "via"));

                Assert.AreSame(firstMessageSenderDest1, secondMessageSenderDest1);
                Assert.AreSame(firstMessageSenderDest2, secondMessageSenderDest2);
                Assert.AreNotSame(firstMessageSenderDest1, firstMessageSenderDest2);
                Assert.AreNotSame(secondMessageSenderDest1, secondMessageSenderDest2);
            }
            finally
            {
                await pool.Close();
                await connection.CloseAsync();
            }
        }

        static string connectionString = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake=";
    }
}