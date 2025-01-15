namespace NServiceBus.Transport.AzureServiceBus;

using System.Text.Json.Serialization;

/// <summary>
/// 
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MigrationTopologyOptions))]
[JsonSerializable(typeof(TopicPerEventTopologyOptions))]
[JsonSerializable(typeof(TopologyOptions))]
public partial class TopologyOptionsSerializationContext : JsonSerializerContext;