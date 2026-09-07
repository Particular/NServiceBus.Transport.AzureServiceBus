namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Wrapper around the per-session session-state available through the service
/// </summary>
sealed class SessionStateEnvelope
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("transport")]
    public TransportSessionState? TransportState { get; set; }

    [JsonPropertyName("user")]
    public UserSessionState? UserState { get; set; }
}

/// <summary>
/// Transport-owned session metadata. Intentionally empty for now: no transport feature currently
/// needs to store anything per session. The section is kept in the envelope shape so that when one
/// does, it can be added here without a breaking change to the wire format or to
/// <see cref="IAzureServiceBusSessionState"/>.
/// </summary>
sealed class TransportSessionState
{
}

/// <summary>
/// User-owned session state. The <see cref="Data"/> payload is stored verbatim as JSON so the
/// transport never needs to know the user's type.
/// </summary>
sealed class UserSessionState
{
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
