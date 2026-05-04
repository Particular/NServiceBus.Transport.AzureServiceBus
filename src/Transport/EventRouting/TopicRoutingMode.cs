namespace NServiceBus.Transport.AzureServiceBus;

/// <summary>
/// Controls how a shared topic is used for publishing and subscribing.
/// The selected mode determines what the transport writes to outgoing messages,
/// how subscriber-side rules or subscriptions are provisioned, and how unsubscribe behaves.
/// </summary>
public enum TopicRoutingMode
{
    /// <summary>
    /// Defers to the effective topology resolution rules.
    /// For publishing, this resolves to per-event <see cref="RoutingOptions.Mode"/> when set,
    /// otherwise to <see cref="FallbackTopicOptions.Mode"/> for unmapped events when a fallback topic is configured,
    /// otherwise to <see cref="NotMultiplexed"/>.
    /// For subscribing, this resolves to the explicitly configured <see cref="SubscriptionEntry.RoutingMode"/> when set,
    /// otherwise to <see cref="FallbackTopicOptions.Mode"/> for unmapped subscriptions when a fallback topic is configured,
    /// otherwise to <see cref="CatchAll"/>.
    /// </summary>
    Default,

    /// <summary>
    /// Publishes to the shared topic without adding multiplexing-specific application properties.
    /// On subscribe, provisions a plain topic subscription with no filter rule so all messages on the topic are forwarded.
    /// On unsubscribe, deletes that subscription.
    /// </summary>
    NotMultiplexed,

    /// <summary>
    /// Adds one boolean Service Bus application property per enclosed message type to the outgoing message.
    /// For example, publishing a message may add <c>ApplicationProperties["Shipping.OrderAccepted"] = true</c>.
    /// On subscribe, provisions a <see cref="Azure.Messaging.ServiceBus.Administration.CorrelationRuleFilter"/> that matches the subscribed type using those application properties.
    /// On unsubscribe, deletes the matching subscription rule.
    /// </summary>
    CorrelationFilter,

    /// <summary>
    /// Publishes without adding extra multiplexing-specific application properties and instead relies on the existing
    /// <c>NServiceBus.EnclosedMessageTypes</c> message header that NServiceBus already writes to the outgoing message.
    /// On subscribe, provisions a <see cref="Azure.Messaging.ServiceBus.Administration.SqlRuleFilter"/> that matches the subscribed type against that header.
    /// On unsubscribe, deletes the matching subscription rule.
    /// </summary>
    SqlFilter,

    /// <summary>
    /// Provisions a plain topic subscription with no filter rule so all messages on the topic are forwarded.
    /// This mode does not add any extra publish-side message data beyond the normal outgoing message headers.
    /// On unsubscribe, deletes that subscription.
    /// </summary>
    CatchAll
}
