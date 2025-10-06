namespace NServiceBus;

using System.Collections.Generic;
using System.Linq;
using Transport.AzureServiceBus.Unmarshalers;

class BatchedUnmarshaller(IEnumerable<IUnmarshalMessages> translators) : IUnmarshalMessages
{
    static UnmarshalledMessage GetDefaultIncomingMessage(MessageToUnmarshal messageToUnmarshal) =>
        new UnmarshalledMessage
        {
            NativeMessageId = messageToUnmarshal.NativeMessageId,
            Headers = messageToUnmarshal.Headers,
            Body = messageToUnmarshal.Body
        };

    public UnmarshalledMessage CreateIncomingMessage(MessageToUnmarshal messageToUnmarshal)
    {
        foreach (var translator in translators)
        {
            if (translator.IsValidMessage(messageToUnmarshal))
            {
                return translator.CreateIncomingMessage(messageToUnmarshal);
            }
        }

        return GetDefaultIncomingMessage(messageToUnmarshal);
    }

    public bool IsValidMessage(MessageToUnmarshal messageToUnmarshal) =>
        translators.Any(t => t.IsValidMessage(messageToUnmarshal));
}