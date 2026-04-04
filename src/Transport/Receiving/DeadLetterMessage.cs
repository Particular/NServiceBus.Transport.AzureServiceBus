namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using Pipeline;
using Transport;

/// <summary>
/// Represents a recoverability action that moves a message to the dead-letter queue.
/// </summary>
public sealed class DeadLetterMessage : RecoverabilityAction
{
    internal DeadLetterMessage(string deadLetterReason, string deadLetterErrorDescription, Dictionary<string, object>? propertiesToModify = null) =>
        deadLetterRequest = new DeadLetterRequest(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

    internal DeadLetterMessage(Exception exception) =>
        deadLetterRequest = new DeadLetterRequest(exception);

    /// <summary>
    /// Stores the dead-letter request on the transport transaction.
    /// </summary>
    public override IReadOnlyCollection<IRoutingContext> GetRoutingContexts(IRecoverabilityActionContext context)
    {
        context.Extensions.Get<TransportTransaction>().Set(deadLetterRequest);
        return [];
    }

    /// <summary>
    /// Indicates the message was handled by dead-lettering it.
    /// </summary>
    public override ErrorHandleResult ErrorHandleResult => ErrorHandleResult.Handled;

    readonly DeadLetterRequest deadLetterRequest;
}
