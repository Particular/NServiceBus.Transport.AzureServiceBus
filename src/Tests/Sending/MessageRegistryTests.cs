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
            var pool = new MessageSenderRegistry(new ServiceBusClient(FakeConnectionString));

            try
            {
                var firstMessageSenderDest1 = pool.GetMessageSender("dest1", null);

                var firstMessageSenderDest2 = pool.GetMessageSender("dest2", null);

                var secondMessageSenderDest1 = pool.GetMessageSender("dest1", null);
                var secondMessageSenderDest2 = pool.GetMessageSender("dest2", null);

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