# Publish/subscribe topology for session-enabled endpoints: alternatives considered

- Status: Accepted
- Decision recorded in: [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493)

#1493 is authoritative for the decision: endpoint-driven forwarders move messages from each ordered subscription to the ordered input queue in one transaction. This record keeps the full set of alternatives so they are not re-investigated from scratch.

## Context

Every existing topology forwards subscription contents to the endpoint input queue. Azure Service Bus does not allow auto-forwarding from a session-enabled (ordered) subscription. Turning sessions off on the subscription loses the ordering guarantee. Consuming directly from subscriptions breaks the NServiceBus model that one input queue, with user-defined concurrency, is the processing boundary of an endpoint.

## Decision

A forwarder per subscription completes the subscription message and sends it to the endpoint's ordered input queue in a single Azure Service Bus transaction. A session-enabled endpoint processes only ordered messages.

## Consequences

- Retries are configured once per endpoint, and the topology stays close to the non-session one.
- An endpoint that needs both ordered and unordered processing must be split into two endpoints (see #1450).

## Alternatives considered

| # | Approach | Why rejected |
| --- | --- | --- |
| 1 | Mix ordered and unordered in one endpoint: unordered input-queue pump + ordered input-queue pump (for retries of ordered failures) + one pump per subscription | Concurrency and retry settings would have to be exposed per pump, with different retry settings for ordered and unordered processing. It does not work with auto-subscribe because the number of subscriptions is unknown at configuration time. It mixes deployment concerns into configuration and loses the "look at one input queue" visibility. |
| 2 | Ordered-only endpoint: ordered input-queue pump + one pump per subscription | Same per-pump concurrency, auto-subscribe, and visibility problems as option 1. |
| 3 | Ordered-only, bundle all ordered event types into one topic with one subscription pump | Forces unrelated event types into one ordering domain, causing contention (e.g. `Customer*` events and `OrderPlaced` events ordered against each other). Reintroduces subscription filtering cost. |
| 4 | Dedicated ordered endpoint per ordered topic (e.g. `Sales.CustomerChanges.Ordered`) consuming from its subscription, plus an ordered input queue for retries | Forces one endpoint per group of ordered events because of service limitations rather than design intent. |
| 5 | Option 4, with separate ordered endpoints for commands and for events | Same as 4. It also conflicts with `RouteToThisEndpoint`, `ReplyTo`, and other features that assume an endpoint's input queue can receive any message, and it is unclear whether that can be blocked reliably at the API level. |

If the forwarder approach needs to be revisited (e.g. because of throughput limits or AMQP link limits), start from this table instead of re-deriving it.
