namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
///  Receiver settings specific to Azure Service Bus receivers
/// </summary>
public class AzureServiceBusReceiveSettings : ReceiveSettings
{
    /// <summary>
    /// <inheritdoc cref="ReceiveSettings"/>
    /// </summary>
    public AzureServiceBusReceiveSettings(
        string id,
        QueueAddress receiveAddress,
        bool usePublishSubscribe,
        bool purgeOnStartup,
        string errorQueue
    ) : base(
        id,
        receiveAddress,
        usePublishSubscribe,
        purgeOnStartup,
        errorQueue
    )
    {
    }

    /// <summary>
    /// Receive from dead-letter queue
    /// </summary>
    public bool DeadLetterQueue { get; init; }

    /// <summary>
    /// Receiver configuration for diagnostics/logging purpose 
    /// </summary>
    public override string ToString() => $"DeadLetterQueue:{DeadLetterQueue}";
}