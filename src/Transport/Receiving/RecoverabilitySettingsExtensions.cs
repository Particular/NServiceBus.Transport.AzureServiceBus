namespace NServiceBus.Transport.AzureServiceBus;

using Features;
using NServiceBus.Configuration.AdvancedExtensibility;

/// <summary>
/// Extension to the core RecoverabilitySettings.
/// </summary>
public static class RecoverabilitySettingsExtensions
{
    extension(RecoverabilitySettings settings)
    {
        /// <summary>
        /// Uses the endpoints native service bus dead letter queue for message failures.
        /// </summary>
        public void MoveErrorsToAzureServiceBusDeadLetterQueue() => settings.GetSettings().EnableFeature<FaultsDeadLetteringFeature>();
    }
}
