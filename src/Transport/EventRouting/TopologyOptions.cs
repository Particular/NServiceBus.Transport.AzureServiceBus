#nullable enable
namespace NServiceBus;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// TODO we probably need some kind of validation method that checks against invalid configurations?
/// </summary>
[JsonDerivedType(typeof(TopologyOptions), typeDiscriminator: "topology-options")]
[JsonDerivedType(typeof(MigrationTopologyOptions), typeDiscriminator: "migration-topology-options")]
[JsonDerivedType(typeof(TopicPerEventTopologyOptions), typeDiscriminator: "topic-per-event-topology-options")]
public class TopologyOptions : IValidatableObject
{
    /// <summary>
    /// 
    /// </summary>
    [JsonInclude]
    public Dictionary<string, string> PublishedEventToTopicsMap { get; init; } = [];

    /// <summary>
    /// 
    /// </summary>
    [JsonInclude]
    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; init; } = [];

    /// <summary>
    /// 
    /// </summary>
    [JsonInclude]
    public Dictionary<string, string> QueueNameToSubscriptionNameMap { get; init; } = [];

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var tooLongTopicNames = PublishedEventToTopicsMap.Values
            .Where(t => t.Length > 260)
            .Union(SubscribedEventToTopicsMap.Values.SelectMany(t => t).Where(t => t.Length > 260))
            .Distinct()
            .ToArray();

        var tooLongSubscriptionNames = QueueNameToSubscriptionNameMap.Values
            .Where(s => s.Length > 50)
            .ToArray();

        if (tooLongTopicNames.Any())
        {
            yield return new ValidationResult(
                $"The following topic name(s) exceed 260 chars: {string.Join(", ", tooLongTopicNames)}",
                [nameof(PublishedEventToTopicsMap), nameof(SubscribedEventToTopicsMap)]
            );
        }

        if (tooLongSubscriptionNames.Any())
        {
            yield return new ValidationResult(
                $"The following subscription name(s) exceed 50 chars: {string.Join(", ", tooLongSubscriptionNames)}",
                [nameof(QueueNameToSubscriptionNameMap)]
            );
        }

        foreach (var validationResult in ValidateCore(validationContext))
        {
            yield return validationResult;
        }
    }

    /// <summary>Determines whether the specified object is valid.</summary>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection that holds failed-validation information.</returns>
    protected virtual IEnumerable<ValidationResult> ValidateCore(ValidationContext validationContext) => [];
}