# Shared AI assistant configuration

`.ai/` is the single source of truth for assistant instructions in this repository. Root `AGENTS.md`,
`CLAUDE.md`, and `.github/copilot-instructions.md` are path-reference adapters. Synopsis uses the Cratis
framework profile: it builds reusable tooling and does not impose application vertical-slice architecture on
its own source.

The focused corpus in this repository documents the product model and the unusual parsing invariants that an
assistant must preserve. Update the canonical rules here, never an adapter.
