#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// 
/// </summary>
public sealed class MigrationTopologyOptions : TopologyOptions
{
    //TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    [Required]
    [StringLength(260, ErrorMessage = "The topic name cannot exceed 260 characters.")]
    public string? TopicToPublishTo { get; set; }

    //TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    [Required]
    [StringLength(260, ErrorMessage = "The topic name cannot exceed 260 characters.")]
    public string? TopicToSubscribeOn { get; set; }

    /// <summary>
    /// Gets whether the current topic topology represents a hierarchy.
    /// </summary>
    [JsonIgnore]
    public bool IsHierarchy => !string.Equals(TopicToPublishTo, TopicToSubscribeOn, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 
    /// </summary>
    [JsonInclude]
    public HashSet<string> EventsToMigrateMap { get; init; } = [];

    /// <summary>
    /// 
    /// </summary>
    [JsonInclude]
    public Dictionary<string, string> SubscribedEventToRuleNameMap { get; init; } = [];

    /// <inheritdoc />
    protected override IEnumerable<ValidationResult> ValidateCore(ValidationContext validationContext)
    {
        var tooLongRuleNames = SubscribedEventToRuleNameMap.Values
            .Where(r => r.Length > 50)
            .ToArray();

        if (tooLongRuleNames.Any())
        {
            yield return new ValidationResult(
                $"The following rule name(s) exceed 50 chars: {string.Join(", ", tooLongRuleNames)}",
                [nameof(SubscribedEventToRuleNameMap)]
            );
        }
    }
}