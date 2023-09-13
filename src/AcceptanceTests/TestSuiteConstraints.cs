namespace NServiceBus.AcceptanceTests
{
    using System.Runtime.CompilerServices;
    using AcceptanceTesting.Support;

    public class TestSuiteConstraints : ITestSuiteConstraints
    {
        public bool SupportsDtc => false;

        public bool SupportsCrossQueueTransactions => true;

        public bool SupportsNativePubSub => true;

        public bool SupportsDelayedDelivery => true;

        public bool SupportsOutbox => true;

        public bool SupportsPurgeOnStartup => false;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointAzureServiceBusTransport();

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointAcceptanceTestingPersistence();

        [ModuleInitializer]
        public static void Initialize() => ITestSuiteConstraints.Current = new TestSuiteConstraints();
    }
}