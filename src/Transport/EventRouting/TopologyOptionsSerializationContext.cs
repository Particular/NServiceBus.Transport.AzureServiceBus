namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// Allows loading the topology information from a json document.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MigrationTopologyOptions))]
[JsonSerializable(typeof(TopologyOptions))]
public partial class TopologyOptionsSerializationContext : JsonSerializerContext;