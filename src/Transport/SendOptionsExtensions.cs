namespace NServiceBus;

using System;
using Extensibility;

public static class SendOptionsExtensions
{
    public static void SetSessionId(this SendOptions options, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var dispatchProperties = options.GetDispatchProperties();
        dispatchProperties.TryAdd("SessionId", sessionId);
    }
}