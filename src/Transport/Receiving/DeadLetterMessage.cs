namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using Logging;
using Pipeline;
using Transport;

/// <summary>
/// Represents a recoverability action that moves a message to the dead-letter queue.
/// </summary>
public sealed class DeadLetterMessage : RecoverabilityAction
{
    internal DeadLetterMessage(string deadLetterReason, string deadLetterErrorDescription, IDictionary<string, object>? propertiesToModify = null) =>
        deadLetterRequest = new DeadLetterRequest(deadLetterReason, deadLetterErrorDescription, propertiesToModify);

    internal DeadLetterMessage() { }

    /// <summary>
    /// Instructs the transport to dead-letter the failing message.
    /// </summary>
    public override IReadOnlyCollection<IRoutingContext> GetRoutingContexts(IRecoverabilityActionContext context)
    {
        context.Extensions.Get<TransportTransaction>().Set(deadLetterRequest ?? CreateFromRecoverabilityContext());

        Logger.Error($"Moving message '{context.FailedMessage.MessageId}' to the dead letter queue because processing failed due to an exception:", context.Exception);

        return [];

        DeadLetterRequest CreateFromRecoverabilityContext()
        {
            var propertiesToModify = new Dictionary<string, object>();

            foreach (var metadata in context.Metadata)
            {
                propertiesToModify[metadata.Key] = metadata.Value;
            }

            return new DeadLetterRequest("NServiceBus", "See application properties", propertiesToModify);
        }
    }

    /// <summary>
    /// Indicates the message was handled by dead-lettering it.
    /// </summary>
    public override ErrorHandleResult ErrorHandleResult => ErrorHandleResult.Handled;

    readonly DeadLetterRequest? deadLetterRequest;

    static readonly ILog Logger = LogManager.GetLogger<DeadLetterMessage>();
}