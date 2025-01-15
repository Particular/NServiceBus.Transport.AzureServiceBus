#nullable enable
namespace NServiceBus;

/// <summary>
/// 
/// </summary>
public class TopicPerEventTopologyOptions : TopologyOptions
{
    /// <summary>
    /// 
    /// </summary>
    public string? SubscriptionName { get; init; }
}