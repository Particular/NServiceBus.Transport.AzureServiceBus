namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;

/// <summary>
/// Requests the transport to dead-letter the message being processed when added to the transport transaction exposed to onError.
/// </summary>
public class DeadLetterRequest(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null)
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
    public Dictionary<string, object> PropertiesToModify { get; } = propertiesToModify ?? [];

    /// <summary>
    /// Dead letters the message being processed due to the provided exception.
    /// </summary>
    public DeadLetterRequest(Exception exception, Dictionary<string, object>? propertiesToModify = null) : this(
        $"{exception.GetType().FullName}{Separator}{exception.Message}",
        exception.StackTrace ?? "No stack trace available",
        propertiesToModify)
    {
    }

    static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    internal const string Separator = " - ";
}