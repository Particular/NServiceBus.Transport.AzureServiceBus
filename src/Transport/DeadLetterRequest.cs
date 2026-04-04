namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;

class DeadLetterRequest(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null)
{
    public string DeadLetterReason { get; } = Truncate(deadLetterReason, 1024);
    public string DeadLetterErrorDescription { get; } = Truncate(deadLetterErrorDescription, 1024);
    public Dictionary<string, object> PropertiesToModify { get; } = propertiesToModify ?? [];

    public DeadLetterRequest(Exception exception, Dictionary<string, object>? propertiesToModify = null) : this(
        $"{exception.GetType().FullName} - {exception.Message}",
        exception.StackTrace ?? "No stack trace available",
        propertiesToModify)
    {
    }

    static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
