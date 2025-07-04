namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Serializable object that defines the migration topology
/// </summary>
[ObsoleteEx(Message = MigrationTopology.ObsoleteMessage, TreatAsErrorFromVersion = MigrationTopology.TreatAsErrorFromVersion, RemoveInVersion = MigrationTopology.RemoveInVersion)]
public sealed class MigrationTopologyOptions : TopologyOptions
{
    /// <summary>
    /// Gets the topic name of the topic where all single-topic events are published to.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
    public required string? TopicToPublishTo { get; init; }

    /// <summary>
    /// Gets the topic name of the topic where all single-topic subscriptions are managed on.
    /// </summary>
    [Required]
    [AzureServiceBusTopics]
    public required string? TopicToSubscribeOn { get; init; }

    /// <summary>
    /// Collection of events that have not yet been migrated to the topic-per-event topology
    /// </summary>
    [ValidMigrationTopology]
    public HashSet<string> EventsToMigrateMap
    {
        get;
        init => field = value ?? [];
    } = [];

    /// <summary>
    /// Maps event full names to non-default rule names.
    /// </summary>
    [AzureServiceBusRules]
    public Dictionary<string, string> SubscribedEventToRuleNameMap
    {
        get;
        init => field = value ?? [];
    } = [];
}