# Delayed retries on session-enabled queues: analysis and spikes

- Status: Accepted (delayed retries not supported; transport-level alternative deferred)
- Decision recorded in: [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493) (section "Delayed retries")

#1493 is authoritative for the decision: the endpoint throws at startup when NServiceBus delayed retries are enabled on a session-enabled endpoint. This record keeps the analysis and spikes for a future task force that may build an order-preserving, transport-level delayed retry mechanism.

## Context

Core delayed retries acknowledge the failing message and schedule a copy. On a session-enabled queue, acknowledging the message makes the next message in the session available, which breaks ordering. Azure Service Bus has no API for deferring a whole session.

Validated assumptions:

- When `MaxDeliveryCount` is exhausted, the message is dead-lettered and the next message in the session becomes available.
- Even without sessions, a message can be dead-lettered if `MaxDeliveryCount` is lower than immediate retries + immediate retries × delayed retries.

## Idea: transport-level delayed retries

When a message exhausts immediate retries, stop processing its session for a period of time instead of acknowledging the message. Two building blocks were considered:

- **Abandon with properties.** `Abandon` can modify application properties, so error headers can record "delayed until T". A consumer that later acquires the message (or reacquires it after lock loss) inspects them and keeps the session blocked. This is deterministic with competing consumers. Each abandon increments `DeliveryCount`, so `MaxDeliveryCount` must be high. The Azure portal caps it at 2000, which at the maximum 5-minute lock duration is about 166 hours of total wait. Lock renewal might extend single waits (unverified).
- **Session blocking.** Hold the session without holding a message by accepting the session in a separate component (unverified for avoiding delivery-count cycling).

Either approach takes a slot from `MaxConcurrentSessions` per blocked session. Many failing sessions can significantly reduce throughput.

## Spikes

| Spike | Result |
| --- | --- |
| Block the session from the message-processing callback | **Not possible.** The processor still holds the session lock, and `ReleaseSession` does not complete immediately. |
| Block via an asynchronously processed in-memory queue | **Not possible.** The processor always re-acquired the session first. |
| Block via a control message sent to a local queue ([spike](https://github.com/poornimanayar/MessageQueueComparison/tree/sessionlockretries/4.MessageSessionReceiver)) | Works only at low concurrency. At about 8, the processor wins the lock. |
| Pull-based message pump that parks a receiver per blocked session ([spike](https://github.com/danielmarbach/AzureServiceBus.DeepDive/tree/session_spike/SubscriptionBridge)) | **Works.** It does not interfere with receiver concurrency and probably has a small endpoint memory footprint. However, it uses one AMQP link per blocked session (5,000 per namespace) and requires a second, entirely different pump implementation for session queues. |
| `Task.Delay` in the callback up to the lock duration, re-checking properties on redelivery, with dynamic concurrency adjustment ([spike](https://github.com/poornimanayar/MessageQueueComparison/tree/taskdelay/4.MessageSessionReceiver)) | **Works**, with small receiver changes and no broker resources besides the session lock. Concurrency is raised before the delay and lowered afterwards to keep active sessions constant. However, each parked message's memory is held on the endpoint, and the safe maximum wait per attempt is unknown. Each attempt consumes a delivery, so the message is eventually dead-lettered. |

## API options considered for a future implementation

1. Transport-level delayed retries on by default; throw if Core delayed retries are enabled.
2. Transport-level delayed retries off by default; throw if Core delayed retries are enabled, with a message pointing to the opt-in.
3. Keep Core defaults; users explicitly disable Core delayed retries and enable transport-level ones; the transport throws if Core delayed retries are still on.

All options assume the transport can detect whether Core delayed retries are enabled.

Whether such a mechanism could be on by default depends on its quality. If it makes dead-lettering more likely than the current non-session defaults, it should not be the default, because NServiceBus has no tooling to monitor or manage the DLQ.

## Consequences of the current decision

Ordering is preserved. Users get immediate retries only, then the message goes to the error queue (see #1450, "Poison messages").
