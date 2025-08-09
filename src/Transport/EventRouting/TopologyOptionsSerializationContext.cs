namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// Allows loading the topology information from a json document.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
#pragma warning disable CS0618 // Type or member is obsolete
[JsonSerializable(typeof(MigrationTopologyOptions))]
#pragma warning restore CS0618 // Type or member is obsolete
[JsonSerializable(typeof(TopologyOptions))]
public partial class TopologyOptionsSerializationContext : JsonSerializerContext;