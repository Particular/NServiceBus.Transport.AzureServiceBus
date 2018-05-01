namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;

    public class ConfigureEndpointAzureServiceBusTransport : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            throw new System.NotImplementedException();
        }

        public Task Cleanup()
        {
            throw new System.NotImplementedException();
        }
    }
}