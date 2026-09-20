namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Default <see cref="IAzureServiceBusSessionState"/> implementation. One instance is created per
/// message being processed and shared between the message and error pipelines through the context bag.
/// </summary>
sealed class AzureServiceBusSessionState(ISessionStateStore store, string sessionId) : IAzureServiceBusSessionState
{
    static readonly JsonSerializerOptions SerializationOptions = new(JsonSerializerDefaults.Web);

    // Loaded at most once per instance (i.e. once per message being processed) and reused for every
    // subsequent Get/Set/Clear call on this instance, the same way a message's saga data is loaded
    // once per message rather than re-fetched on every access.
    SessionStateEnvelope? envelope;
    bool dirty;

    public async Task<T?> Get<T>(CancellationToken cancellationToken = default)
    {
        var current = await Load(cancellationToken).ConfigureAwait(false);

        if (current.UserState is not { } user || user.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return default;
        }

        return user.Data.Deserialize<T>(SerializationOptions);
    }

    public async Task Set<T>(T state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var current = await Load(cancellationToken).ConfigureAwait(false);

        current.UserState = new UserSessionState
        {
            ContentType = "application/json",
            Type = typeof(T).FullName,
            Data = JsonSerializer.SerializeToElement(state, SerializationOptions)
        };

        dirty = true;
    }

    public async Task Clear(CancellationToken cancellationToken = default)
    {
        var current = await Load(cancellationToken).ConfigureAwait(false);
        if (current.UserState is null)
        {
            return;
        }

        current.UserState = null;
        dirty = true;
    }

    /// <summary>
    /// Persists pending session-state changes, if any. Called by the session pump right before the message is completed.
    /// </summary>
    public async Task Flush(CancellationToken cancellationToken = default)
    {
        if (!dirty || envelope is null)
        {
            return;
        }

        await store.SetSessionStateAsync(SessionStateEnvelopeSerializer.Write(envelope), cancellationToken).ConfigureAwait(false);
        dirty = false;
    }

    async Task<SessionStateEnvelope> Load(CancellationToken cancellationToken)
    {
        if (envelope is not null)
        {
            return envelope;
        }

        var raw = await store.GetSessionStateAsync(cancellationToken).ConfigureAwait(false);
        envelope = SessionStateEnvelopeSerializer.Read(raw, sessionId);
        return envelope;
    }
}
