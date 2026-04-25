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

## Testing Philosophy

- Tests are executable specifications, not coverage theater.
- Prefer the smallest test that gives meaningful confidence.
- Tests should be deterministic, isolated, and runnable by both humans and AI agents.
- Tests should verify observable behavior, not implementation details.
- Do not weaken, delete, or rewrite existing assertions just to make a change pass.
- If an existing test appears incorrect, explain why and make the test change explicit.
- Do not mix broad refactors with unrelated test rewrites.

## Test Taxonomy

Use these terms consistently.

### Unit Test

A unit test verifies one narrow behavior in isolation.

Rules:
- No real network, database, filesystem, message broker, or external service.
- Time, randomness, IDs, and environment variables must be controlled through test seams.
- Mock or stub ports only when needed.
- Prefer real domain objects over excessive mocks.
- Use for domain invariants, value objects, pure functions, validators, policies, and edge cases.

### Component / Module Test

A component test verifies multiple in-process modules together.

Examples:
- Use case + domain model + fake repository.
- Handler + validator + fake ports.
- React/Vue component mounted with test providers.
- Frontend feature module with mocked API adapter.

Rules:
- External dependencies are fake, mocked, in-memory, or stubbed.
- These tests may use framework rendering or dependency injection.
- These tests are not proof that real external dependencies work.

### Slice Test

A slice test verifies a vertical path through part of the app.

Examples:
- HTTP route -> controller -> use case -> domain with fake persistence.
- Message consumer -> handler -> application service with fake ports.
- UI action -> feature service -> mocked API adapter.

Rules:
- Use slice tests to protect meaningful behavior during refactors.
- Prefer slice tests over broad E2E tests when they provide the same confidence.
- Clearly identify whether dependencies are fake-backed or real.

### Fake-Backed Integration Test

A fake-backed integration test wires multiple real app components together but replaces external dependencies.

Examples:
- API pipeline with in-memory test server.
- Repository-like behavior using in-memory database provider.
- Service using a fake HTTP server.
- Message flow using an in-memory broker.

Rules:
- Name these as fake-backed, component, or slice tests.
- Do not claim these prove compatibility with the real database, broker, or external API.
- Use them for routing, orchestration, serialization shape, validation, and basic flow confidence.

### Real-Dependency Integration Test

A real-dependency integration test verifies code against the actual class of dependency used in production.

Examples:
- Repository/DAO against a real database container.
- Migration test against a real database container.
- Message publisher/consumer against a real broker container.
- Storage adapter against a real local emulator or controlled test instance.
- HTTP adapter against a verified provider sandbox when appropriate.

Rules:
- Prefer ephemeral local dependencies such as containers or official emulators.
- Tests must control schema, data, and cleanup.
- Tests must not depend on production systems.
- Shared staging dependencies are allowed only when explicitly tagged non-hermetic/external.
- Use these tests for SQL semantics, migrations, transactions, indexes, serialization, broker behavior, and provider-specific behavior.

### Contract Test

A contract test verifies that a consumer and provider agree on request/response or message/event shape.

Rules:
- Use consumer contracts for APIs/events this codebase consumes.
- Providers should verify the contracts they are expected to satisfy.
- Contract tests are preferred over broad cross-service E2E tests for microservice compatibility.
- OpenAPI/AsyncAPI/schema checks are useful, but examples of actual consumer expectations are stronger.
- Contract tests do not replace provider unit/component tests.

### End-to-End / Acceptance Test

An E2E test verifies a critical user or system workflow through the running application.

Rules:
- Keep E2E coverage focused on critical paths.
- Do not test every business rule through E2E.
- Prefer API/component/slice tests for permutations and edge cases.
- E2E tests must control data and avoid uncontrolled third-party dependencies.
- E2E tests should be tagged separately from fast PR tests.

### Characterization Test

A characterization test captures current behavior before changing legacy code.

Rules:
- Add characterization tests before risky refactors.
- Preserve public behavior first; improve internals second.
- If behavior is intentionally changed, update tests and explain the behavior change.

### Property-Based Test

A property-based test verifies invariants across many generated inputs.

Use for:
- Date/time rules.
- Pricing/rating calculations.
- Permission logic.
- Parsers and serializers.
- State transitions.
- Domain invariants.

Rules:
- Use deterministic seeds when failures need reproduction.
- Include the failing seed or minimized case in failure output when supported.

### Golden Master / Approval / Snapshot Test

A golden master, approval, or snapshot test compares output to an approved baseline.

Use for:
- Legacy behavior.
- Generated documents.
- Serialized payloads.
- Stable UI screenshots.
- Complex text or report output.

Rules:
- Snapshots must be reviewed as behavior, not blindly updated.
- Do not update snapshots or visual baselines merely to make tests pass.
- Keep snapshots focused and readable where possible.
- Prefer semantic assertions over giant snapshots when practical.

### Mutation Test

A mutation test evaluates whether tests fail when production code is intentionally changed.

Rules:
- Use mutation testing for high-value domain logic where correctness matters.
- Do not require mutation tests for every routine change unless the project explicitly does.
- Mutation score is a signal, not an absolute goal.

## Dependency Realism Labels

Every test suite should make dependency realism clear.

Use these labels in folders, tags, categories, or naming:

- `unit`: no external dependency.
- `component`: multiple in-process modules; external dependencies faked.
- `slice`: vertical app path; dependency mode must be clear.
- `contract`: consumer/provider contract verification.
- `integration-real`: real database, broker, filesystem, emulator, or container.
- `integration-fake`: fake-backed integration; not proof of real dependency behavior.
- `e2e`: full workflow through running app.
- `characterization`: legacy behavior capture.
- `mutation`: test-suite quality check.
- `external`: uses shared staging, sandbox, or third-party dependency and may be slower/flakier.

Do not call a test `integration-real` if it uses only mocks, fake servers, or in-memory providers.

## Database Testing Rules

- In-memory database tests are fake-backed tests.
- SQLite in-memory tests are relational fake-backed tests unless SQLite is the production database.
- Real database integration tests should use the production database engine or closest supported equivalent.
- Use real-dependency integration tests for:
  - migrations,
  - transaction behavior,
  - SQL syntax,
  - provider-specific queries,
  - indexes and constraints,
  - concurrency behavior,
  - raw SQL,
  - ORM mapping behavior.
- Use component or slice tests with fake persistence for application orchestration and business rule permutations.
- Each database test must control seed data and cleanup.
- Tests must not depend on production data.

## External API and Messaging Tests

- Use contract tests for service-to-service HTTP, event, and message compatibility.
- Use fake servers for consumer-side behavior tests.
- Use provider verification to ensure the real provider satisfies consumer expectations.
- Use real-dependency integration tests for serialization, authentication setup, retry policies, broker configuration, and adapter behavior.
- Do not call live third-party services from normal PR test suites.
- Tests that call shared external systems must be tagged `external`.

## Test Data Rules

- Prefer explicit test data builders, mothers, factories, or fixtures over large opaque shared fixtures.
- Each test should make its important inputs and expected outcomes clear.
- Avoid cross-test state.
- Avoid test order dependencies.
- Use stable clocks, IDs, random seeds, and environment configuration.
- Failure messages should identify the behavior that broke.

## Refactoring Rules

- Before changing legacy behavior, add characterization tests around the current behavior unless the task is explicitly a bug fix.
- Run the smallest relevant test first, then broader suites as needed.
- Do not perform large structural refactors and behavior changes in the same diff unless unavoidable.
- After changes, run relevant build, test, lint, and typecheck checks.
- If verification cannot be run, report exactly what was not run and why.

## Project CLAUDE.md Requirements

Each project must define its actual commands.

Required command entries:
- Build:
- Typecheck, if applicable:
- Lint/static analysis:
- Unit tests:
- Component/module tests:
- Contract tests, if applicable:
- Real-dependency integration tests, if applicable:
- E2E/Playwright tests, if applicable:
- Mutation tests, if applicable:

Each project must also document:
- Test framework conventions.
- Test naming/tagging conventions.
- Whether in-memory providers are used and how they are classified.
- How real external dependencies are started locally.
- Which test suites are expected for normal PR-sized changes.
