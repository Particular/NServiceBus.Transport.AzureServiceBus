#nullable enable
namespace NServiceBus;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// TODO we probably need some kind of validation method that checks against invalid configurations?
/// </summary>
[JsonDerivedType(typeof(TopologyOptions), typeDiscriminator: "topology-options")]
[JsonDerivedType(typeof(MigrationTopologyOptions), typeDiscriminator: "migration-topology-options")]
[JsonDerivedType(typeof(TopicPerEventTopologyOptions), typeDiscriminator: "topic-per-event-topology-options")]
public class TopologyOptions
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
}