namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Particular.Obsoletes;

/// <summary>
/// Serializable object that defines the migration topology
/// </summary>
[ObsoleteMetadata(Message = MigrationObsoleteMessages.ObsoleteMessage, TreatAsErrorFromVersion = MigrationObsoleteMessages.TreatAsErrorFromVersion, RemoveInVersion = MigrationObsoleteMessages.RemoveInVersion)]
[Obsolete("The migration topology is intended to be used during a transitional period, facilitating the migration from the single-topic topology to the topic-per-event topology. The migration topology will eventually be phased out over subsequent releases. Should you face challenges during migration, please reach out to |https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/issues/1170|. Will be treated as an error from version 7.0.0. Will be removed in version 8.0.0.", false)]
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

    // NOTE: explicitly set to true always and ignored from JSON, since MigrationTopology is already obsolete and we don't want to have a fallback naming strategy
    /// <inheritdoc cref="TopologyOptions.ThrowIfUnmappedEventTypes" />
    [JsonIgnore]
    public new bool ThrowIfUnmappedEventTypes => true;
}