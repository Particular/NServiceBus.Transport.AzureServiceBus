namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;
    using Transport.AzureServiceBus.AcceptanceTests;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => false;

        public bool SupportsCrossQueueTransactions => true;

        public bool SupportsNativePubSub => true;

        public bool SupportsNativeDeferral => true;

        public bool SupportsOutbox => true;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointAzureServiceBusTransport();

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();
    }
}