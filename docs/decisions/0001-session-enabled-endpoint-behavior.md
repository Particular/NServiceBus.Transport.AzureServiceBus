# Behavior and boundaries of session-enabled endpoints

- Status: Accepted
- Related: [#1450](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450) (send/receive), [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493) (publish/subscribe), [ADR 0002](0002-publish-subscribe-topology-for-sessions.md), [ADR 0003](0003-delayed-retries-on-session-enabled-queues.md)

This record collects the decisions about session-enabled (ordered) endpoints that are not already explained in #1450 and #1493. Those pull requests remain authoritative for the decisions they describe: separate ordered and unordered endpoints, mixing both kinds in one conversation, releasing the session after each message, concurrency, poison messages, transaction modes, forwarders, forwarding configuration and error handling, quota handling, and not supporting delayed retries.

## Context

Azure Service Bus sessions can be used for workflows, ordering, and multiplexing (sub-queuing). NServiceBus already supports workflows through sagas, so the transport uses sessions only for **ordered delivery**. Each decision below follows from that scope or from broker and SDK behavior the task force observed.

## Decisions

### Consume with the session processor, not per session

The SDK supports two consumption models: accepting a specific session and receiving from it, or using `ServiceBusSessionProcessor`, which cycles through available sessions.

**Decision:** The transport uses `ServiceBusSessionProcessor` and does not expose per-session receiving.

**Consequences:** Users cannot prioritize or pin specific sessions, or use sessions as sub-queues within one input queue. Separate concerns belong in separate queues/endpoints, consistent with the rest of NServiceBus.

**Alternative considered:** Per-session consumption. It would enable sub-queuing and cross-session priority, which is outside the ordered-delivery scope.

### Sessions are enabled for the whole endpoint

**Decision:** One transport-level setting enables sessions. In installer mode, the queue creator creates the main input queue **and all satellite queues** with sessions required.

**Consequences:** There is no per-queue opt-in. This can be revisited if data points show a need for it.

### Ordering flags on topics and subscriptions

Observed broker/SDK behavior:

- A publisher can set `SessionId` and publish to a topic regardless of whether the topic has *Enforce message ordering* set; the service does not discriminate.
- A session-requiring subscription can consume from a topic without *Enforce message ordering*, as long as messages carry a `SessionId`.
- A session-requiring subscription can only be consumed with the session processor.

Because the subscriber creates the topic (or the first subscriber does), the result could depend on which subscriber happened to create it.

**Decision:** Topics used by ordered subscribers have *Enforce message ordering* set, and all subscriptions under an ordered topic are created consistently. The resulting topology is the same regardless of which subscriber created the topic.

### Non-session endpoints cannot subscribe to session-enabled subscriptions

A forwarder could move messages from a session-enabled subscription into a non-session input queue, but ordering would only hold up to the forwarder. Users would likely read "session-enabled subscription" as "ordered processing", which would be a much stronger guarantee than the one provided.

**Decision:** Only session-enabled endpoints subscribe through session-enabled subscriptions; non-session endpoints only subscribe through non-session subscriptions.

**Revisit when:** a concrete use case for "ordered up to the input queue" appears.

### Subscription-creation rights are required

Forwarders start when `Subscribe` is called (normally through auto-subscribe at startup). Starting forwarders is coupled to creating subscriptions.

**Decision:** Session-enabled mode requires an identity that is allowed to create subscriptions. This is a documented requirement.

**Consequences:** Session-enabled endpoints cannot currently run in a low-privilege mode where subscriptions are provisioned externally and the endpoint has no management rights.

**Revisit when:** users report this as a blocker. A possible option is a transport setting that disables subscription creation but still starts forwarders.

### Forwarder throttling: no special handling

Forwarding has no handler processing time, so a forwarder draining a large subscription backlog (for example, at startup) can exceed namespace throughput limits and be throttled.

**Decision:** No mitigation. Session-enabled endpoints are not expected to need very high throughput.

**Revisit when:** users report throttling caused by forwarders.

### Partitioning

On partitioned entities, Azure Service Bus uses the session ID as the partition key. If both are set, they must be identical, or the service throws `InvalidOperationException` ([docs](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-partitioning#using-a-partition-key)).

**Decision:** Support what the service provides. The transport adds no partition-key behavior.

### Delayed delivery is supported

**Decision:** Delayed delivery (as opposed to delayed retries) is supported on session-enabled endpoints. Spikes found no reason to block it.

### Sending to session-enabled endpoints

The `SessionId` dispatch property can be set by:

- a pipeline behavior that copies it from the incoming message;
- `SendOptions`, `PublishOptions`, and `ReplyOptions` extension methods, when sending outside a handler or from a handler processing a message from a non-session queue.

## Observed broker and SDK behavior (reference)

- **The DLQ of a session-enabled queue is not session-enabled.** It can only be received with a regular (non-session) receiver. Per-session order in the DLQ can only be reconstructed from `SequenceNumber`.
- **Native DLQ breaks ordering.** When `MaxDeliveryCount` is exceeded, the message is dead-lettered and the next message in the session becomes available. The service cannot dead-letter a whole session on failure, as it can on TTL expiry. This is tracked in [Azure/azure-service-bus#675](https://github.com/Azure/azure-service-bus/issues/675).
- **SDK bug with `ReceiveAndDelete` and session IDs.** Receiving a message that has a `SessionId` from a session-required entity in `ReceiveAndDelete` mode returns `LockedUntil` = year 9999. The SDK then overflows while calculating `cancel-after`. In `PeekLock` mode, or without a session ID, `LockedUntil` is meaningful. The transport does not hit this in supported modes because `TransportTransactionMode.None` is not supported with sessions. For this reason the `TransportTransactionMode.None` case of the `Should_not_roll_back_send_if_DLQ_receive_is_rolled_back` transport test is disabled in the session-enabled transport tests.
