#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>
/// 
/// </summary>
public sealed class MigrationTopologyOptions : TopologyOptions
{
    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
    public string? TopicToPublishTo { get; set; }

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
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
    [AzureServiceBusRules]
    public Dictionary<string, string> SubscribedEventToRuleNameMap { get; init; } = [];
}