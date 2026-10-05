namespace NServiceBus;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides safe access to the user-owned portion of the Azure Service Bus session state for the
/// session the message currently being processed belongs to.
/// </summary>
public interface IAzureServiceBusSessionState
{
    /// <summary>
    /// Reads and deserializes the user-owned session state using the default, reflection-based
    /// JSON serialization behavior.
    /// </summary>
    /// <typeparam name="T">The type the stored payload should be deserialized into.</typeparam>
    /// <returns>
    /// The deserialized state, or <see langword="default"/> when no user state has been stored for the session.
    /// </returns>
    /// <remarks>
    /// This overload relies on runtime reflection to deserialize <typeparamref name="T"/> and is not compatible
    /// with trimming or Native AOT. Use the <see cref="Get{T}(JsonSerializerOptions, CancellationToken)"/> or
    /// <see cref="Get{T}(JsonTypeInfo{T}, CancellationToken)"/> overload instead when targeting those scenarios.
    /// </remarks>
    [RequiresUnreferencedCode("JSON deserialization of the state type might require types that cannot be statically analyzed. Use the Get<T>(JsonSerializerOptions, CancellationToken) or Get<T>(JsonTypeInfo<T>, CancellationToken) overload instead.")]
    [RequiresDynamicCode("JSON deserialization of the state type might require runtime code generation. Use the Get<T>(JsonSerializerOptions, CancellationToken) or Get<T>(JsonTypeInfo<T>, CancellationToken) overload instead.")]
    Task<T?> Get<T>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and deserializes the user-owned session state using the supplied <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <typeparam name="T">The type the stored payload should be deserialized into.</typeparam>
    /// <param name="serializerOptions">
    /// The options to deserialize with. Supply a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
    /// to keep deserialization trimming- and Native AOT-safe.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while loading.</param>
    /// <returns>
    /// The deserialized state, or <see langword="default"/> when no user state has been stored for the session.
    /// </returns>
    Task<T?> Get<T>(JsonSerializerOptions serializerOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and deserializes the user-owned session state using the supplied <see cref="JsonTypeInfo{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type the stored payload should be deserialized into.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated metadata to deserialize <typeparamref name="T"/> with.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while loading.</param>
    /// <returns>
    /// The deserialized state, or <see langword="default"/> when no user state has been stored for the session.
    /// </returns>
    Task<T?> Get<T>(JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and stores the user-owned session state using the default, reflection-based JSON
    /// serialization behavior, preserving any transport-owned metadata already present in the session state.
    /// </summary>
    /// <typeparam name="T">The type of the payload being stored.</typeparam>
    /// <param name="state">The state to persist. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while persisting.</param>
    /// <remarks>
    /// This overload relies on runtime reflection to serialize <typeparamref name="T"/> and is not compatible
    /// with trimming or Native AOT. Use the <see cref="Set{T}(T, JsonSerializerOptions, CancellationToken)"/> or
    /// <see cref="Set{T}(T, JsonTypeInfo{T}, CancellationToken)"/> overload instead when targeting those scenarios.
    /// </remarks>
    [RequiresUnreferencedCode("JSON serialization of the state type might require types that cannot be statically analyzed. Use the Set<T>(T, JsonSerializerOptions, CancellationToken) or Set<T>(T, JsonTypeInfo<T>, CancellationToken) overload instead.")]
    [RequiresDynamicCode("JSON serialization of the state type might require runtime code generation. Use the Set<T>(T, JsonSerializerOptions, CancellationToken) or Set<T>(T, JsonTypeInfo<T>, CancellationToken) overload instead.")]
    Task Set<T>(T state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and stores the user-owned session state using the supplied <see cref="JsonSerializerOptions"/>,
    /// preserving any transport-owned metadata already present in the session state.
    /// </summary>
    /// <typeparam name="T">The type of the payload being stored.</typeparam>
    /// <param name="state">The state to persist. Must not be <see langword="null"/>.</param>
    /// <param name="serializerOptions">
    /// The options to serialize with. Supply a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
    /// to keep serialization trimming- and Native AOT-safe.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while persisting.</param>
    Task Set<T>(T state, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and stores the user-owned session state using the supplied <see cref="JsonTypeInfo{T}"/>,
    /// preserving any transport-owned metadata already present in the session state.
    /// </summary>
    /// <typeparam name="T">The type of the payload being stored.</typeparam>
    /// <param name="state">The state to persist. Must not be <see langword="null"/>.</param>
    /// <param name="jsonTypeInfo">The source-generated metadata to serialize <typeparamref name="T"/> with.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while persisting.</param>
    Task Set<T>(T state, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the user-owned session state, preserving any transport-owned metadata already present
    /// in the session state.
    /// </summary>
    Task Clear(CancellationToken cancellationToken = default);
}
