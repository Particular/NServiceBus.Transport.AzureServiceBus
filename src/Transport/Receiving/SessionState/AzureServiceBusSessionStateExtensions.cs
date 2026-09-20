namespace NServiceBus;

using System;

/// <summary>
/// Extension methods for accessing Azure Service Bus session state while handling a message.
/// </summary>
public static class AzureServiceBusSessionStateExtensions
{
    /// <summary>
    /// Gets the <see cref="IAzureServiceBusSessionState"/> for the session the message currently being
    /// processed belongs to.
    /// </summary>
    /// <param name="context">The message handler context.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when session state is not available for the current message, which is the case unless
    /// the endpoint has sessions enabled (<see cref="AzureServiceBusTransport.EnableSessions"/>) and
    /// the message was received from a session-enabled queue.
    /// </exception>
    public static IAzureServiceBusSessionState GetSessionState(this IMessageHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Extensions.TryGet(out IAzureServiceBusSessionState? sessionState))
        {
            throw new InvalidOperationException(
                "Azure Service Bus session state is not available for the current message. It can only be " +
                "accessed when the endpoint has sessions enabled (AzureServiceBusTransport.EnableSessions = true) " +
                "and the message was received from a session-enabled queue.");
        }

        return sessionState;
    }
}
