#nullable enable
namespace NServiceBus;

/// <summary>
/// 
/// </summary>
public class TopicPerEventTopologyOptions : TopologyOptions
{
    public string? SubscriptionName { get; init; }
}