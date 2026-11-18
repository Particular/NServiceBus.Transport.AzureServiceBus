namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Describes the transport behavior in regard to sessions
/// </summary>
public sealed class SessionOptions
{
    /// <summary>
    /// Is session support enabled
    /// </summary>
    public bool IsEnabled { get; private set; }

    SessionOptions() { }

    /// <summary>
    /// Enables the session support
    /// </summary>
    /// <returns></returns>
    public static SessionOptions Enabled() =>
        new()
        {
            IsEnabled = true
        };

    /// <summary>
    /// Disables the session support
    /// </summary>
    public static SessionOptions Disabled { get; } = new()
    {
        IsEnabled = false
    };
}