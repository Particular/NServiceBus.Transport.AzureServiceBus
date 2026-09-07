namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads and writes the <see cref="SessionStateEnvelope"/> to and from the raw <see cref="BinaryData"/>
/// blob exchanged with Azure Service Bus. Every read returns a full envelope and every write persists
/// a full envelope, keeping the transport and user sections isolated from each other.
/// </summary>
static class SessionStateEnvelopeSerializer
{
    // WhenWritingNull keeps unused sections (e.g. the transport section) out of the persisted blob.
    static readonly JsonSerializerOptions SerializerOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static SessionStateEnvelope Read(BinaryData? rawSessionState, string sessionId)
    {
        if (rawSessionState is null)
        {
            return new SessionStateEnvelope();
        }

        var bytes = rawSessionState.ToMemory();
        if (bytes.IsEmpty)
        {
            return new SessionStateEnvelope();
        }

        try
        {
            return JsonSerializer.Deserialize<SessionStateEnvelope>(bytes.Span, SerializerOptions)
                   ?? throw NotOurEnvelope(sessionId, innerException: null);
        }
        catch (JsonException ex)
        {
            throw NotOurEnvelope(sessionId, ex);
        }
    }

    public static BinaryData Write(SessionStateEnvelope envelope)
    {
        envelope.Version = SessionStateEnvelope.CurrentVersion;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        return BinaryData.FromBytes(bytes);
    }

    // The session state already carries state this transport did not write so we throw
    static Exception NotOurEnvelope(string sessionId, Exception? innerException) =>
        new Exception(
            $"The Azure Service Bus session state for session '{sessionId}' is not in the format written by " +
            $"{nameof(IAzureServiceBusSessionState)}. Session state must only be read and written through " +
            $"{nameof(IAzureServiceBusSessionState)} (available via context.Extensions in a message handler); " +
            "writing to it directly, for example by calling ServiceBusSessionReceiver.SetSessionStateAsync, " +
            "is not supported and is treated as corrupted the next time it is accessed through this API.",
            innerException);
}
