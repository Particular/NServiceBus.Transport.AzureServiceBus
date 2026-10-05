namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Default <see cref="IAzureServiceBusSessionState"/> implementation. One instance is created per
/// message being processed and shared between the message and error pipelines through the context bag.
/// </summary>
sealed class AzureServiceBusSessionState(ISessionStateStore store, string sessionId) : IAzureServiceBusSessionState
{
    static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web);

    // Loaded at most once per instance (i.e. once per message being processed) and reused for every
    // subsequent Get/Set/Clear call on this instance, the same way a message's saga data is loaded
    // once per message rather than re-fetched on every access.
    SessionStateEnvelope? envelope;
    bool dirty;

    [RequiresUnreferencedCode("JSON deserialization of the state type might require types that cannot be statically analyzed. Use the Get<T>(JsonSerializerOptions, CancellationToken) or Get<T>(JsonTypeInfo<T>, CancellationToken) overload instead.")]
    [RequiresDynamicCode("JSON deserialization of the state type might require runtime code generation. Use the Get<T>(JsonSerializerOptions, CancellationToken) or Get<T>(JsonTypeInfo<T>, CancellationToken) overload instead.")]
    public Task<T?> Get<T>(CancellationToken cancellationToken = default) =>
        Get<T>(DefaultSerializerOptions, cancellationToken);

    public async Task<T?> Get<T>(JsonSerializerOptions serializerOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var data = await LoadUserData(cancellationToken).ConfigureAwait(false);
        return data is { } value ? value.Deserialize<T>(serializerOptions) : default;
    }

    public async Task<T?> Get<T>(JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        var data = await LoadUserData(cancellationToken).ConfigureAwait(false);
        return data is { } value ? value.Deserialize(jsonTypeInfo) : default;
    }

    [RequiresUnreferencedCode("JSON serialization of the state type might require types that cannot be statically analyzed. Use the Set<T>(T, JsonSerializerOptions, CancellationToken) or Set<T>(T, JsonTypeInfo<T>, CancellationToken) overload instead.")]
    [RequiresDynamicCode("JSON serialization of the state type might require runtime code generation. Use the Set<T>(T, JsonSerializerOptions, CancellationToken) or Set<T>(T, JsonTypeInfo<T>, CancellationToken) overload instead.")]
    public Task Set<T>(T state, CancellationToken cancellationToken = default) =>
        Set(state, DefaultSerializerOptions, cancellationToken);

    public async Task Set<T>(T state, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        await StoreUserState<T>(JsonSerializer.SerializeToElement(state, serializerOptions), cancellationToken).ConfigureAwait(false);
    }

    public async Task Set<T>(T state, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        await StoreUserState<T>(JsonSerializer.SerializeToElement(state, jsonTypeInfo), cancellationToken).ConfigureAwait(false);
    }

    async Task<JsonElement?> LoadUserData(CancellationToken cancellationToken)
    {
        var current = await Load(cancellationToken).ConfigureAwait(false);

        if (current.UserState is not { } user || user.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return user.Data;
    }

    async Task StoreUserState<T>(JsonElement data, CancellationToken cancellationToken)
    {
        var current = await Load(cancellationToken).ConfigureAwait(false);

        current.UserState = new UserSessionState
        {
            Type = typeof(T).FullName,
            Data = data
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
