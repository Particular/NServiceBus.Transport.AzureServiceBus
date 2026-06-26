namespace NServiceBus;

using System;
using Extensibility;

/// <summary>
/// Extensions for <see cref="SendOptions"/>.
/// </summary>
public static class SendOptionsExtensions
{
    /// <summary>
    /// Extends the SendOptions class to allow adding a session id to dispatch properties
    /// </summary>
    /// <param name="options">SendOptions</param>
    /// <param name="sessionId">The session id to add to the dispatch properties</param>
    /// <exception cref="ArgumentException"></exception>
    public static void SetSessionId(this SendOptions options, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var dispatchProperties = options.GetDispatchProperties();
        dispatchProperties.TryAdd("SessionId", sessionId);
    }
}