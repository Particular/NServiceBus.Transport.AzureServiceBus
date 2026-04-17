namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;

/// <summary>
/// Requests the transport to dead-letter the message being processed when added to the transport transaction exposed to onError.
/// </summary>
/// <remarks>
/// This class has two usages:
/// 1. With Core recoverability: Use RecoverabilityAction.DeadLetter() (this will add DeadLetterRequest to the transport transaction automatically)
/// 2. Raw transport: Create and set DeadLetterRequest directly in TransportTransaction via onError.
/// </remarks>
public class DeadLetterRequest(string deadLetterReason, string deadLetterErrorDescription, IDictionary<string, object>? propertiesToModify = null)
{
    /// <summary>
    /// Reason for the message being dead lettered, truncated to max 1024 characters.
    /// </summary>
    public string DeadLetterReason { get; } = Truncate(deadLetterReason, 1024);

    /// <summary>
    /// Additional details for the message being dead lettered, truncated to max 1024 characters.
    /// </summary>
    public string DeadLetterErrorDescription { get; } = Truncate(deadLetterErrorDescription, 1024);

    /// <summary>
    /// Properties to update on the message to be dead lettered.
    /// </summary>
    public IDictionary<string, object>? PropertiesToModify { get; } = propertiesToModify;

    /// <summary>
    /// Dead letters the message being processed due to the provided exception.
    /// </summary>
    public DeadLetterRequest(Exception exception, IDictionary<string, object>? propertiesToModify = null) : this(
        $"{exception.GetType().FullName}{Separator}{exception.Message}",
        exception.StackTrace ?? "No stack trace available",
        propertiesToModify)
    {
    }

    static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    internal const string Separator = " - ";
}