#nullable enable
namespace NServiceBus;

/// <summary>
/// Represents instructions on how to subscribe for an event
/// </summary>
public class SubscriptionInfo
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
    /// Optional rule name to create.
    /// </summary>
    public string? RuleName { get; init; }

    /// <summary>
    /// Optional rule filter to use when creating a rule.
    /// </summary>
    public string? RuleFilter { get; init; }
}