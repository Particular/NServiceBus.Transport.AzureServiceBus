namespace NServiceBus;

using System;
using Extensibility;

/// <summary>
/// Extensions for <see cref="PublishOptions"/>.
/// </summary>
public static class PubishOptionsExtensions
{
    /// <summary>
    /// Extends the PublishOptions class to allow adding a session id to dispatch properties
    /// </summary>
    /// <param name="options">PublishOptions</param>
    /// <param name="sessionId">The session id to add to the dispatch properties</param>
    /// <exception cref="ArgumentException"></exception>
    public static void SetSessionId(this PublishOptions options, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var dispatchProperties = options.GetDispatchProperties();
        dispatchProperties.TryAdd("SessionId", sessionId);
    }
}