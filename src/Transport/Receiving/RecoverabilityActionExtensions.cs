namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;

/// <summary>
/// Provides Azure Service Bus-specific recoverability actions.
/// </summary>
public static class RecoverabilityActionExtensions
{
    extension(RecoverabilityAction _)
    {
        /// <summary>
        /// Creates a recoverability action that moves the message to the dead-letter queue with the specified details.
        /// </summary>
        public static DeadLetterMessage DeadLetter(string deadLetterReason, string deadLetterErrorDescription, IDictionary<string, object>? propertiesToModify = null)
            => new(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

        /// <summary>
        /// Creates a recoverability action that moves the message to the dead-letter queue including all fault metadata.
        /// </summary>
        public static DeadLetterMessage DeadLetter() => new();
    }
}