# Organization Engineering Rules

> **Role:** simulated managed-policy CLAUDE.md content. In a real org rollout this file would be deployed to the OS-level managed-policy path (`C:\Program Files\ClaudeCode\CLAUDE.md` on Windows, `/etc/claude-code/CLAUDE.md` on Linux, `/Library/Application Support/ClaudeCode/CLAUDE.md` on macOS) via MDM/GPO/Ansible and auto-load into every Claude Code session on the machine. For this testbed it is checked into the repo at `ENTERPRISE.md` and pulled in via `@import` from the project `CLAUDE.md` — same effective behavior, repo-scoped instead of machine-scoped.
>
> **Editing:** in production this is owned by the platform/architecture team and changes ship through the managed-policy distribution. Per-project overrides go in the project `CLAUDE.md`, never here.

## Architecture Priorities

- Optimize for clear boundaries, testability, maintainability, and safe AI-assisted refactoring.
- Follow Clean/Hexagonal dependency direction:
  - Domain depends on nothing outside domain.
  - Application depends on domain and ports.
  - Infrastructure implements ports.
  - API/UI delegates to application/use cases.
- Do not introduce framework, HTTP, ORM, database, or vendor SDK types into domain code.
- Prefer explicit, boring, navigable code over clever indirection.

## Use Cases

- Put business operations behind named use cases, handlers, interactors, or application services.
- Controllers, routes, resolvers, message consumers, and UI actions should delegate to use cases.
- Use cases orchestrate work.
- Domain models enforce business rules.
- Infrastructure performs side effects.

## Abstractions

Introduce an interface/port only when it:
- crosses an external boundary,
- has multiple implementations,
- protects domain/application from infrastructure,
- provides a necessary test seam,
- or represents a domain capability.

Do not create interfaces only because a class exists.

## DDD Policy

- Use rich domain models for complex business rules.
- Simple CRUD services may use simpler models.
- Aggregates protect invariants.
- Domain events are for same-bounded-context side effects.
- Integration events are for cross-service or cross-bounded-context communication after commit.

## Testing

Testing standards live in [`ENTERPRISE-TESTING.md`](ENTERPRISE-TESTING.md), imported below. That file covers test taxonomy, the `Layer` / `Size` / `DependencyMode` classification, dependency-realism rules (including in-memory ≠ real-database), naming, folder organization, CI placement, agentic AI rules, anti-patterns, and per-project `CLAUDE.md` requirements.

@ENTERPRISE-TESTING.md
