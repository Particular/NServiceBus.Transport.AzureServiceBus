namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class AzureServiceBusTransportTests
    {
        [Test]
        public void Throws_when_not_initialized()
        {
            var transport = new AzureServiceBusTransport();

            var exception = Assert.ThrowsAsync<Exception>(async () => await transport.Initialize(null, [], []));
            StringAssert.Contains("The transport has not been initialized. Either provide a connection string or a fully qualified namespace and token credential.", exception.Message);
        }
    }
}