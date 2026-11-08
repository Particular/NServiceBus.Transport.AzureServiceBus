# Engineering context

## About this repository

NServiceBus.Transport.AzureServiceBus is the NServiceBus transport for Azure Service Bus. It produces the `NServiceBus.Transport.AzureServiceBus` NuGet package and the `asb-transport` command-line tool. For local build and test steps see the [README.md](../README.md).

- `src/Transport/` — the transport: administration, sending, receiving, event routing, and configuration
- `src/CommandLine/` — the `asb-transport` tool that creates queues, topics, and subscriptions outside the endpoint
- `src/Tests/`, `src/TransportTests/`, `src/*AcceptanceTests*/` — unit, transport, and acceptance tests, including the session-enabled variants
- `docs/` — engineering context and architecture decision records
- `.github/workflows/` — CI pipelines

## Start here

- [Azure Service Bus transport documentation](https://docs.particular.net/transports/azure-service-bus/) — public documentation entry point
- [README.md](../README.md) — how to build, run, and test locally
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

- [Session-enabled endpoints](session-enabled-endpoints.md) — how ordered delivery with sessions behaves beyond what #1450 and #1493 record: session processor, endpoint-wide sessions, ordering flags, subscription rights, forwarder throttling, partitioning, delayed delivery, `SessionId` on outgoing messages, and observed broker and SDK behavior
- [Forward ordered subscriptions to the input queue through endpoint-driven forwarders](decisions/2026-09-29-publish-subscribe-topology-for-sessions.md) — which publish/subscribe topologies for sessions were rejected and why
- [Do not support delayed retries on session-enabled endpoints](decisions/2026-09-29-delayed-retries-on-session-enabled-endpoints.md) — why session-enabled endpoints throw when delayed retries are enabled, and which spikes and API options exist for a transport-level alternative

## Upstream contracts

- [NServiceBus: infrastructure setup](https://github.com/Particular/NServiceBus/blob/master/docs/infrastructure-setup.md) — what core expects the transport to create for installers, sending addresses, event topics, and subscriptions
- [NServiceBus: message dispatchers](https://github.com/Particular/NServiceBus/blob/master/docs/message-dispatchers.md) — what core expects from `MessageDispatcher` for batching, dispatch consistency, delivery properties, and replayed operations
- [NServiceBus: message receivers](https://github.com/Particular/NServiceBus/blob/master/docs/message-receivers.md) — what core expects from `MessagePump` and `SessionsEnabledMessagePump` for concurrency, stopping, cancellation, and long-running handlers
- [NServiceBus: recoverability](https://github.com/Particular/NServiceBus/blob/master/docs/recoverability.md) — what core expects from the message pumps when `OnMessage` fails, including how to act on `ErrorHandleResult`
- [NServiceBus: logging](https://github.com/Particular/NServiceBus/blob/master/docs/logging.md) — when core guarantees an `ILoggerFactory`, and how components should log
- [NServiceBus: trimming and Native AOT](https://github.com/Particular/NServiceBus/blob/master/docs/trimming-and-aot.md) — when the transport may claim trimming and Native AOT compatibility, and which analyzers to enable otherwise
- [NServiceBus: for downstream components](https://github.com/Particular/NServiceBus/blob/master/docs/README.md#for-downstream-components) — every contract NServiceBus documents for transports, persisters, and serializers

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Decisions recorded in pull requests

A pull request is listed here only when it is the canonical record for a decision area: it establishes a durable constraint or convention, or rejects an alternative likely to return, and no `docs/` file or ADR covers it. Bug fixes and routine changes are not listed; recover them from `git log` and `gh pr view`.

- Ordered and unordered processing use separate endpoints but can mix in one conversation; the session is released after each message; poison messages, concurrency, and the unsupported `TransportTransactionMode.None` with sessions — [#1450](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450)
- Publish/subscribe with sessions: forwarder topology, forwarding configuration and error handling, quota handling, and no delayed retries — [#1493](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493)
- Session state API; the description is empty, and the rationale is in the review threads — [#1503](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1503)
- With installers enabled and `ThrowOnMissingTopicWhenPublishing` off, the dispatcher creates missing topics before the first publish, sets up every topic in a dispatch call before sending, and makes one delivery attempt — [#1561](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1561)
- Dispatch failures are logged once, in the layer that handles them; missing-topic warnings are logged once per topic per endpoint start, and transient `onError` failures in the pumps log at Warn — [#1562](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1562)
- ServiceControl keeps receive properties such as the session ID as `ServiceControl.ReceiveProperties.*` pseudo-headers and restores them as dispatch properties on retry — [ServiceControl#5962](https://github.com/Particular/ServiceControl/pull/5962)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
