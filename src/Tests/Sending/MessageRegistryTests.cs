namespace NServiceBus.Transport.AzureServiceBus.Tests.Sending
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class MessageRegistryTests
    {
        [Test]
        public async Task Should_get_cached_sender_per_destination()
        {
            var client = new ServiceBusClient(FakeConnectionString);
            var pool = new MessageSenderRegistry();

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1", client);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2", client);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1", client);
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2", client);

                Assert.Multiple(() =>
                {
                    Assert.That(secondMessageSenderDest1, Is.SameAs(firstMessageSenderDest1));
                    Assert.That(secondMessageSenderDest2, Is.SameAs(firstMessageSenderDest2));
                    Assert.That(firstMessageSenderDest2, Is.Not.SameAs(firstMessageSenderDest1));
                });
                Assert.That(secondMessageSenderDest2, Is.Not.SameAs(secondMessageSenderDest1));
            }
            finally
            {
                await pool.Close();
            }
        }

        static readonly string FakeConnectionString = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake=";
    }
}