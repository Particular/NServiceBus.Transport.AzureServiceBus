namespace NServiceBus
{
    /// <summary>
    /// Provides configuration extension methods for API backwards compatibility.
    /// </summary>
    public static class AzureServiceBusSettingsExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use <see cref="AzureServiceBusTransport"/>.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static AzureServiceBusSettings UseTransport<TTransport>(this EndpointConfiguration endpointConfiguration, string connectionString)
            where TTransport : AzureServiceBusTransport // scope this extension method to AzureServiceBusTransport
        {
            var transportSettings = new AzureServiceBusTransport(connectionString);
            var routing = endpointConfiguration.UseTransport(transportSettings);
            return new AzureServiceBusSettings(transportSettings, routing);
        }
    }
}