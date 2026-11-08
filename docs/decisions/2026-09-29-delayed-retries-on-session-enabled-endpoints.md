# Do not support delayed retries on session-enabled endpoints

Implemented in [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493) (section "Delayed retries"), which remains the record for the decision. This record keeps the analysis and spikes for a future task force that may build an order-preserving delayed retry mechanism in the transport.

## Context

Core delayed retries acknowledge the failing message and schedule a copy. On a session-enabled queue, acknowledging the message makes the next message in the session available, which breaks ordering. Azure Service Bus has no API for deferring a whole session.

Validated assumptions:

- When `MaxDeliveryCount` is exhausted, the service dead-letters the message and makes the next message in the session available.
- Even without sessions, a message can be dead-lettered if `MaxDeliveryCount` is lower than immediate retries + immediate retries × delayed retries.

## Decision

A session-enabled endpoint throws at startup when NServiceBus delayed retries are enabled. The transport does not provide its own delayed retries.

## Consequences

Ordering is preserved. Failed messages get immediate retries only and then move to the error queue, as described under "Poison messages" in [#1450](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450). The loss of delayed retries is accepted. The transport-level alternative below is deferred, not rejected.

## Alternative approaches

### Delay retries in the transport by blocking the session

When a message exhausts immediate retries, the transport stops processing its session for a period of time instead of acknowledging the message. Two building blocks were considered:

- Abandon with properties. `Abandon` can modify application properties, so error headers can record "delayed until T". A consumer that later acquires the message, or reacquires it after lock loss, inspects them and keeps the session blocked. This is deterministic with competing consumers. Each abandon increments `DeliveryCount`, so `MaxDeliveryCount` must be high. The Azure portal caps it at 2000, which at the maximum 5-minute lock duration is about 166 hours of total wait. Lock renewal might extend single waits; this is unverified.
- Session blocking. A separate component accepts the session and holds it without holding a message. Whether this avoids cycling the delivery count is unverified.

Either approach takes a slot from `MaxConcurrentSessions` per blocked session, so many failing sessions can significantly reduce throughput.

| Spike | Result |
| --- | --- |
| Block the session from the message-processing callback | Not possible. The processor still holds the session lock, and `ReleaseSession` does not complete immediately. |
| Block through an asynchronously processed in-memory queue | Not possible. The processor always reacquired the session first. |
| Block through a control message sent to a local queue ([spike](https://github.com/poornimanayar/MessageQueueComparison/tree/sessionlockretries/4.MessageSessionReceiver)) | Works only at low concurrency. At a concurrency of about 8, the processor wins the lock. |
| Pull-based message pump that parks a receiver per blocked session ([spike](https://github.com/danielmarbach/AzureServiceBus.DeepDive/tree/session_spike/SubscriptionBridge)) | Works. It does not interfere with receiver concurrency and probably has a small endpoint memory footprint. However, it uses one AMQP link per blocked session (5,000 per namespace) and requires a second, entirely different pump implementation for session queues. |
| `Task.Delay` in the callback up to the lock duration, rechecking properties on redelivery, with dynamic concurrency adjustment ([spike](https://github.com/poornimanayar/MessageQueueComparison/tree/taskdelay/4.MessageSessionReceiver)) | Works, with small receiver changes and no broker resources besides the session lock. Concurrency is raised before the delay and lowered afterwards to keep active sessions constant. However, the endpoint holds each parked message in memory, and the safe maximum wait per attempt is unknown. Each attempt consumes a delivery, so the message is eventually dead-lettered. |

API options considered for a future implementation:

1. Transport-level delayed retries on by default; throw if Core delayed retries are enabled.
2. Transport-level delayed retries off by default; throw if Core delayed retries are enabled, with a message that points to the opt-in.
3. Keep Core defaults; users explicitly disable Core delayed retries and enable the transport-level ones; the transport throws if Core delayed retries are still on.

All options assume the transport can detect whether Core delayed retries are enabled.

Whether such a mechanism could be on by default depends on its quality. If it makes dead-lettering more likely than the current non-session defaults, it should not be the default, because NServiceBus has no tooling to monitor or manage the dead-letter queue.
