namespace NServiceBus
{
    using Settings;
    using Transport;
    using Transport.AzureServiceBus;

    /// <summary>Transport definition for Azure Service Bus.</summary>
    public class AzureServiceBusTransport : TransportDefinition
    {
        /// <summary>
        /// Initializes all the factories and supported features for the transport.
        /// </summary>
        /// <param name="settings">An instance of the current settings.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>The supported factories.</returns>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString) => new AzureServiceBusTransportInfrastructure(settings, connectionString);

        /// <summary>
        /// Gets an example connection string to use when reporting the lack of a configured connection string to the user.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage { get; } = "Endpoint=sb://[namespace].servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[secret_key]";
    }
}