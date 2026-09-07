namespace NServiceBus;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides safe access to the user-owned portion of the Azure Service Bus session state for the
/// session the message currently being processed belongs to.
/// </summary>
public interface IAzureServiceBusSessionState
{
    /// <summary>
    /// Reads and deserializes the user-owned session state.
    /// </summary>
    /// <typeparam name="T">The type the stored payload should be deserialized into.</typeparam>
    /// <returns>
    /// The deserialized state, or <see langword="default"/> when no user state has been stored for the session.
    /// </returns>
    Task<T?> Get<T>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and stores the user-owned session state, preserving any transport-owned metadata
    /// already present in the session state.
    /// </summary>
    /// <typeparam name="T">The type of the payload being stored.</typeparam>
    /// <param name="state">The state to persist. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while persisting.</param>
    Task Set<T>(T state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the user-owned session state, preserving any transport-owned metadata already present
    /// in the session state.
    /// </summary>
    Task Clear(CancellationToken cancellationToken = default);
}
