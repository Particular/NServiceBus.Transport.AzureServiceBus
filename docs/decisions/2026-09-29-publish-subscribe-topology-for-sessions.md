# Forward ordered subscriptions to the input queue through endpoint-driven forwarders

Implemented in [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493), which remains the record for the decision. This record keeps the full set of alternatives so they are not investigated again from the start.

## Context

Every existing topology forwards subscription contents to the endpoint input queue. Azure Service Bus does not allow auto-forwarding from a session-enabled (ordered) subscription. Turning sessions off on the subscription loses the ordering guarantee. Consuming directly from subscriptions breaks the NServiceBus model that one input queue, with user-defined concurrency, is the processing boundary of an endpoint.

## Decision

A forwarder per subscription completes the subscription message and sends it to the endpoint's ordered input queue in a single Azure Service Bus transaction. A session-enabled endpoint processes only ordered messages.

## Consequences

- Retries are configured once per endpoint, and the topology stays close to the non-session one.
- An endpoint that needs both ordered and unordered processing must be split into two endpoints. [#1450](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450) accepts this split for send/receive as well.
- Forwarders need subscription-creation rights and can be throttled while they drain a backlog. [Session-enabled endpoints](../session-enabled-endpoints.md) records both as accepted, with the conditions for revisiting them.

If the forwarder approach needs to be revisited, for example because of throughput limits or AMQP link limits, start from the alternatives below.

## Alternative approaches

| # | Approach | Why rejected |
| --- | --- | --- |
| 1 | Mix ordered and unordered in one endpoint: unordered input-queue pump, ordered input-queue pump (for retries of ordered failures), and one pump per subscription | Concurrency and retry settings would have to be exposed per pump, with different retry settings for ordered and unordered processing. It does not work with auto-subscribe because the number of subscriptions is unknown at configuration time. It mixes deployment concerns into configuration and loses the visibility of one input queue. |
| 2 | Ordered-only endpoint: ordered input-queue pump and one pump per subscription | Same per-pump concurrency, auto-subscribe, and visibility problems as option 1. |
| 3 | Ordered-only endpoint that bundles all ordered event types into one topic with one subscription pump | Forces unrelated event types into one ordering domain, which causes contention; for example, `Customer*` events and `OrderPlaced` events are ordered against each other. Reintroduces subscription filtering cost. |
| 4 | Dedicated ordered endpoint per ordered topic (for example, `Sales.CustomerChanges.Ordered`) that consumes from its subscription, plus an ordered input queue for retries | Forces one endpoint per group of ordered events because of service limitations rather than design intent. |
| 5 | Option 4, with separate ordered endpoints for commands and for events | Same as option 4. It also conflicts with `RouteToThisEndpoint`, `ReplyTo`, and other features that assume an endpoint's input queue can receive any message, and it is unclear whether that can be blocked reliably at the API level. |
