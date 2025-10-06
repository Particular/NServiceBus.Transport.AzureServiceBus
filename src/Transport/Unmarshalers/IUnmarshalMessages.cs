namespace NServiceBus.Transport.AzureServiceBus.Unmarshalers;

using Transport;

/// <summary>
/// Defines methods to unmarshal and validate incoming transport messages.
/// </summary>
interface IUnmarshalMessages
{
    /// <summary>
    /// Creates a <see cref="UnmarshalledMessage"/> from the given <see cref="MessageToUnmarshal"/>.
    /// </summary>
    /// <param name="messageToUnmarshal">The incoming message.</param>
    /// <returns>The created <see cref="UnmarshalledMessage"/>.</returns>
    UnmarshalledMessage CreateIncomingMessage(MessageToUnmarshal messageToUnmarshal);

    /// <summary>
    /// Validates the given <see cref="MessageContext"/>.
    /// </summary>
    /// <param name="messageToUnmarshal">The incoming message.</param>
    /// <returns>True if the message is valid; otherwise, false.</returns>
    bool IsValidMessage(MessageToUnmarshal messageToUnmarshal);
}