# Engineering context

This page points to sources that help explain the transport's design and behavior.

## Start here

- [Azure Service Bus transport documentation](https://docs.particular.net/transports/azure-service-bus/)
- [Contributing guidance](../CONTRIBUTING.md)

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Ordered delivery (sessions)

- [Send/receive with sessions: endpoint boundaries, session release, concurrency, poison messages, transaction modes](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1450)
- [Publish/subscribe with sessions: forwarders, forwarding configuration, quota handling, delayed retries](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1493)
- [Session-enabled endpoint behavior and broker observations](decisions/0001-session-enabled-endpoint-behavior.md)
- [Publish/subscribe topology alternatives](decisions/0002-publish-subscribe-topology-for-sessions.md)
- [Delayed retries analysis and spikes](decisions/0003-delayed-retries-on-session-enabled-queues.md)
- [Session state](https://github.com/Particular/NServiceBus.Transport.AzureServiceBus/pull/1503)
- [Retrying failed messages from ServiceControl with receive properties (session ID)](https://github.com/Particular/ServiceControl/pull/5962)
