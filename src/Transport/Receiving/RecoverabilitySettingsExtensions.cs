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
        /// Uses the native service bus dead letter queues for message failures.
        /// </summary>
        public void UseAzureServiceBusDeadLetterQueueForFailures() => settings.GetSettings().EnableFeature<FaultsDeadLetteringFeature>();
    }
}