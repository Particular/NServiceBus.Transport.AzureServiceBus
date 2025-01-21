#nullable enable
namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// 
/// </summary>
public class MigrationTopologyOptions : TopologyOptions
{
    //TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public string? TopicToPublishTo { get; set; }

    //TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
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
}