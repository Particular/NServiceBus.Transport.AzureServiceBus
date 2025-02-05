#nullable enable
namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Represents instructions on how to create a rule
/// </summary>
public readonly record struct RuleInfo
{
    /// <summary>
    /// Optional rule name to create.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Optional rule filter to use when creating a rule.
    /// </summary>
    public string Filter { get; init; }
}