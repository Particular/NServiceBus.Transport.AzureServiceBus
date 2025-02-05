#nullable enable

namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Represents instructions on how to subscribe for an event
/// </summary>
public readonly record struct SubscriptionInfo
{
    /// <summary>
    /// Name of the topic to subscribe to.
    /// </summary>
    public string Topic { get; init; }

    /// <summary>
    /// Name of the subscription to create/modify.
    /// </summary>
    public string SubscriptionName { get; init; }

    /// <summary>
    /// Optional rule to create.
    /// </summary>
    public RuleInfo? Rule { get; init; }
}