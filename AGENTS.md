## Engineering context

Start here when investigating existing behavior or making a change whose rationale may need to outlive the pull request:

- [Engineering context](docs/README.md)
- [Architecture and design decisions](docs/decisions/)
- [Contributing guidance](CONTRIBUTING.md)

Prefer public records close to the implementation. Keep `docs/README.md` current when a canonical public source is added, replaced, or retired; update its links rather than copying rationale into the index.

1. Identify the decision area and the precise question. For example: "Why does a session-enabled endpoint throw at startup when delayed retries are enabled?"
2. Start with the public source linked by `docs/README.md`. Read the current public contract, relevant code, tests, and linked pull requests or ADRs. Follow relevant pointer comments as routing hints; the linked source remains canonical. A Git commit can identify a pull request number without containing its description; retrieve it with `gh pr view <number> --repo Particular/NServiceBus.Transport.AzureServiceBus`. If the pull request cannot be retrieved, report that rather than infer its rationale. Public records control public behavior and contribution requirements.
3. Decide whether the current work creates rationale worth preserving beyond the pull request. Propose an ADR in `docs/decisions/` when the change introduces an important constraint, affects multiple components, or rejects an alternative likely to return. When an approved private context root is available, use `$PARTICULAR_CONTEXT_ROOT/cross-component/` for a decision that affects multiple components and `$PARTICULAR_CONTEXT_ROOT/repositories/NServiceBus.Transport.AzureServiceBus/` for a repository-specific decision that cannot be public. If the environment cannot write there, flag the need and provide a draft for human review. Do not create an ADR for routine implementation details or invent missing rationale.
4. Check private context only when this environment explicitly provides `$PARTICULAR_CONTEXT_ROOT/repositories/NServiceBus.Transport.AzureServiceBus/README.md`. Follow applicable links to `cross-component/` and read a linked private record only when it is marked `agent-access: allowed`. Do not inspect a sibling `../Platform` checkout. Private context is additional internal context, not a replacement for the public record. Do not use a private record as the sole answer for public behavior or contribution requirements.
5. If sources conflict, do not resolve the conflict by inference. Use the current implementation and public contract for external behavior, report the conflict, and ask the record owner when it affects the decision.
6. In the response or pull request, cite the records consulted, distinguish evidence from inference, and state when relevant private context was unavailable or unauthorized.
