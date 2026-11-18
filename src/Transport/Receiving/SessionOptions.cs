namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Determines how subscription forwarders are configured
/// </summary>
public enum SessionForwardingMode
{
    /// <summary>
    /// Create subscription forwarders for subscribed events only
    /// </summary>
    SubscribedEventsOnly,
    /// <summary>
    /// Create subscription forwarders for all mapped events (implicit or explicit)
    /// </summary>
    AllMappedEvents,
}

/// <summary>
/// Describes the transport behavior in regard to sessions
/// </summary>
public sealed class SessionOptions
{
    /// <summary>
    /// Is session support enabled
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Should forward all
    /// </summary>
    public SessionForwardingMode ForwardingMode { get; private set; }

    SessionOptions() { }

    /// <summary>
    /// Enables the session support
    /// </summary>
    /// <param name="forwardingMode">How to configure subscription forwarders</param>
    /// <returns></returns>
    public static SessionOptions Enabled(SessionForwardingMode forwardingMode = SessionForwardingMode.SubscribedEventsOnly) =>
        new()
        {
            IsEnabled = true,
            ForwardingMode = forwardingMode
        };

    /// <summary>
    /// Disables the session support
    /// </summary>
    public static SessionOptions Disabled { get; } = new()
    {
        IsEnabled = false
    };
}