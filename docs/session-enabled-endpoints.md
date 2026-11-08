# Session-enabled endpoints

The transport uses Azure Service Bus sessions only for ordered delivery. Sessions can also serve workflows and multiplexing (sub-queues), but NServiceBus already covers workflows with sagas. Each behavior below follows from that scope or from broker and SDK behavior that the ordered-delivery task force observed.

[#1450](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450) (send/receive) and [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493) (publish/subscribe) remain the records for the decisions they describe: separate ordered and unordered endpoints, mixing both kinds in one conversation, releasing the session after each message, concurrency, poison messages, transaction modes, forwarders, forwarding configuration and error handling, quota handling, and not supporting delayed retries. This page covers the behavior those pull requests do not explain.

## Consumption with the session processor

The SDK supports two consumption models: accepting a specific session and receiving from it, or using `ServiceBusSessionProcessor`, which cycles through available sessions. The transport uses `ServiceBusSessionProcessor` and does not expose per-session receiving.

Users cannot prioritize or pin specific sessions, or use sessions as sub-queues within one input queue. Separate concerns belong in separate queues and endpoints, as elsewhere in NServiceBus. Per-session consumption was rejected because sub-queuing and cross-session priority are outside the ordered-delivery scope.

## Sessions for the whole endpoint

One transport-level setting enables sessions. In installer mode, the queue creator creates the main input queue and all satellite queues with sessions required. There is no per-queue opt-in; revisit this if data points show a need for it.

## Ordering flags on topics and subscriptions

Observed broker and SDK behavior:

- A publisher can set `SessionId` and publish to a topic whether or not the topic has *Enforce message ordering* set.
- A session-requiring subscription can consume from a topic without *Enforce message ordering*, as long as messages carry a `SessionId`.
- A session-requiring subscription can only be consumed with the session processor.

The first subscriber creates the topic, so the topology could otherwise depend on which subscriber created it. Topics used by ordered subscribers therefore have *Enforce message ordering* set, and all subscriptions under an ordered topic are created consistently.

## No non-session subscribers on session-enabled subscriptions

Only session-enabled endpoints subscribe through session-enabled subscriptions, and non-session endpoints subscribe only through non-session subscriptions. A forwarder could move messages from a session-enabled subscription into a non-session input queue, but ordering would hold only up to the forwarder. Users would likely read "session-enabled subscription" as "ordered processing", a much stronger guarantee than the one provided.

Revisit when a concrete use case for "ordered up to the input queue" appears.

## Subscription-creation rights

Forwarders start when `Subscribe` is called, normally through auto-subscribe at startup, so starting forwarders is coupled to creating subscriptions. Session-enabled mode requires an identity that is allowed to create subscriptions, and this is a documented requirement.

Session-enabled endpoints cannot run in a low-privilege mode where subscriptions are provisioned externally and the endpoint has no management rights. Revisit when users report this as a blocker. One option is a transport setting that disables subscription creation but still starts forwarders.

## Forwarder throttling

Forwarding has no handler processing time. A forwarder that drains a large subscription backlog, for example at startup, can exceed namespace throughput limits and be throttled. The transport does not mitigate this, because session-enabled endpoints are not expected to need very high throughput.

Revisit when users report throttling caused by forwarders.

## Partitioning

On partitioned entities, Azure Service Bus uses the session ID as the partition key. If both are set, they must be identical, or the service throws `InvalidOperationException` ([Azure documentation](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-partitioning#using-a-partition-key)). The transport supports what the service provides and adds no partition-key behavior.

## Delayed delivery

Session-enabled endpoints support delayed delivery, as opposed to delayed retries. Spikes found no reason to block it. For delayed retries, see [Delayed retries on session-enabled endpoints](decisions/2026-09-29-delayed-retries-on-session-enabled-endpoints.md).

## Sending to session-enabled endpoints

These set the `SessionId` dispatch property:

- a pipeline behavior that copies it from the incoming message;
- the `SendOptions`, `PublishOptions`, and `ReplyOptions` extension methods, when sending outside a handler or from a handler that processes a message from a non-session queue.

## Observed broker and SDK behavior

- The dead-letter queue (DLQ) of a session-enabled queue is not session-enabled. Only a regular (non-session) receiver can receive from it, and per-session order in the DLQ can only be reconstructed from `SequenceNumber`.
- The native DLQ breaks ordering. When `MaxDeliveryCount` is exceeded, the service dead-letters the message and makes the next message in the session available. The service cannot dead-letter a whole session on failure, as it can on TTL expiry. [Azure/azure-service-bus#675](https://github.com/Azure/azure-service-bus/issues/675) tracks this.
- The SDK fails for `ReceiveAndDelete` with session IDs. Receiving a message with a `SessionId` from a session-required entity in `ReceiveAndDelete` mode returns a `LockedUntil` in the year 9999, and the SDK overflows while it calculates `cancel-after`. In `PeekLock` mode, or without a session ID, `LockedUntil` is meaningful. The transport does not support `TransportTransactionMode.None` with sessions, so supported modes do not hit this. For the same reason, the session-enabled transport tests disable the `TransportTransactionMode.None` case of `Should_not_roll_back_send_if_DLQ_receive_is_rolled_back`.
