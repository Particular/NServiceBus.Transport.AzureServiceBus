namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>
/// Serializable object that defines the migration topology
/// </summary>
public sealed class MigrationTopologyOptions : TopologyOptions
{
    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all single-topic events are published to.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
    public string? TopicToPublishTo { get; init; }

    /// TODO: Change to required/init once the Fody Obsolete problem is fixed
    /// <summary>
    /// Gets the topic name of the topic where all single-topic subscriptions are managed on.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
    public string? TopicToSubscribeOn { get; init; }

    /// <summary>
    /// Collection of events that have not yet been migrated to the topic-per-event topology
    /// </summary>
    [JsonInclude]
    [ValidMigrationTopology]
    public HashSet<string> EventsToMigrateMap { get; init; } = [];

    /// <summary>
    /// Maps event full names to non-default rule names.
    /// </summary>
    [JsonInclude]
    [AzureServiceBusRules]
    public Dictionary<string, string> SubscribedEventToRuleNameMap { get; init; } = [];
}