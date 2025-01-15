#nullable enable
namespace NServiceBus;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// TODO we probably need some kind of validation method that checks against invalid configurations?
/// </summary>
[JsonDerivedType(typeof(MigrationTopologyOptions))]
[JsonDerivedType(typeof(TopicPerEventTopologyOptions))]
public class TopologyOptions
{
    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> PublishedEventToTopicsMap { get; } = [];

    /// <summary>
    /// 
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, HashSet<string>> SubscribedEventToTopicsMap { get; } = [];
}