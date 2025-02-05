namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Queue address qualifiers
/// </summary>
public static class QueueAddressQualifier
{
    /// <summary>
    /// Qualifier that identifies the Azure Service Bus native dead-letter subqueue 
    /// </summary>
    public const string DeadLetterQueue = "$DeadLetterQueue";
}