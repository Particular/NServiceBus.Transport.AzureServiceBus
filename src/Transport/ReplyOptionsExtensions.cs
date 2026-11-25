namespace NServiceBus;

using System;
using Extensibility;

/// <summary>
/// Extensions for <see cref="ReplyOptions"/>.
/// </summary>
public static class ReplyOptionsExtensions
{
    /// <summary>
    /// Extends the ReplyOptions class to allow adding a session id to dispatch properties
    /// </summary>
    /// <param name="options">ReplyOptions</param>
    /// <param name="sessionId">The session id to add to the dispatch properties</param>
    /// <exception cref="ArgumentException"></exception>
    public static void SetSessionId(this ReplyOptions options, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var dispatchProperties = options.GetDispatchProperties();
        dispatchProperties.TryAdd("SessionId", sessionId);
    }
}
