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
    /// <summary>
    /// Gets the topic name of the topic where all events are published to.
    /// </summary>
    public required string TopicToPublishTo { get; init; }

    /// <summary>
    /// Gets the topic name of the topic where all subscriptions are managed on.
    /// </summary>
    public required string TopicToSubscribeOn { get; init; }

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
}