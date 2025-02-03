#nullable enable

namespace NServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// 
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MigrationTopologyOptions))]
[JsonSerializable(typeof(TopologyOptions))]
public partial class TopologyOptionsSerializationContext : JsonSerializerContext;