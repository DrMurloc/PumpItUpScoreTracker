# Enterprise Agentic AI Testing Guide — Code-Driven Test Suites Only

> **Role:** simulated managed-policy content, imported from [`ENTERPRISE.md`](ENTERPRISE.md). See that file for the org-policy framing. This document is owned by the org architecture team; per-project carve-outs go in the project `CLAUDE.md`, never here.

## Purpose

This guide standardizes testing terminology and decision rules for **tests that live in the codebase** and are executed by a normal test runner, build pipeline, or code-driven harness.

Examples of acceptable execution models:

- C# tests run by NUnit, xUnit, MSTest, or equivalent.
- Java/Kotlin tests run by JUnit, TestNG, Spock, or equivalent.
- JavaScript/TypeScript tests run by Vitest, Jest, Mocha, Playwright Test, Cypress Component Test, or equivalent.
- Python tests run by Pytest, unittest, Hypothesis, or equivalent.
- Go tests run by `go test`.
- Contract tests, browser tests, property tests, approval tests, and mutation tests when they are defined in code and invoked from repository commands.

The standard is intentionally **technology-agnostic**. Specific tools are examples, not enterprise mandates.

---

## Scope

This guide covers:

- Unit tests.
- Component tests.
- Slice tests.
- Integration tests using fakes, in-memory dependencies, local servers, containers, or other ephemeral infrastructure.
- Contract tests.
- API/service boundary tests.
- UI component tests.
- UI end-to-end tests driven by code.
- Characterization tests.
- Approval/golden-master tests.
- Property-based tests.
- Mutation testing.
- Test suite tagging, naming, dependency classification, and CI placement.

This guide does **not** cover:

- Manual QA scripts.
- External black-box monitoring.
- Production observability alerts.
- Vendor-only testing platforms with no in-repo test definitions.
- One-off exploratory testing.
- Load testing that exists entirely outside the codebase.
- Security scans that are not represented as executable code or pipeline-owned test definitions.

These may still be valuable, but they are outside this standard.

---

## Core Principle

Choose the **lowest-cost code-driven test** that gives meaningful confidence.

Do not add a higher-level test when a lower-level test would catch the same regression with less cost, less flakiness, and clearer failure output.

The AI agent must ask:

1. What behavior changed?
2. What risk needs to be protected?
3. What is the cheapest test layer that would catch a real regression?
4. Does the dependency behavior matter?
5. Does the browser/runtime/framework behavior matter?
6. Will the failure clearly explain what broke?
7. Can this test run reliably in CI?

---

## Testing Philosophy

The principles below apply across every test type in this guide.

- Tests are executable specifications, not coverage theater.
- Tests should be deterministic, isolated, and runnable by both humans and AI agents.
- Tests should verify observable behavior, not implementation details.
- **Do not weaken, delete, or rewrite existing assertions just to make a change pass.** If an existing test appears incorrect, explain why and make the test change explicit and isolated from the behavioral change.
- **Do not mix broad refactors with unrelated test rewrites in the same diff.**
- Time, randomness, IDs, and environment configuration must be controlled through test seams.
- Failure messages should identify the behavior that broke.

---

# Standard Test Classification

Every test should be classifiable across three dimensions:

```text
Layer: Unit | Component | Slice | Integration | Contract | API | UIComponent | UIE2E | Characterization | Approval | Property | Mutation
Size: Small | Medium | Large
DependencyMode: None | TestDouble | InMemory | EphemeralInfra | SharedEnv | ThirdParty
```

## Size

| Size | Meaning | Typical Execution |
|---|---|---|
| `Small` | Fast, deterministic, in-process, no real infrastructure | Normal unit/component test runner |
| `Medium` | May start app runtime, local server, browser component host, in-memory dependency, or ephemeral infrastructure | Normal test runner or test harness |
| `Large` | May drive a real browser, multiple services, deployed test env, real network calls, or full user flows | Dedicated CI stage or scheduled suite |

## Dependency Mode

| Mode | Meaning |
|---|---|
| `None` | No external dependency. Pure in-process behavior. |
| `TestDouble` | Uses mocks, stubs, spies, fakes, or simulators. |
| `InMemory` | Uses an in-process substitute such as an in-memory repository, in-memory message bus, or in-memory database. |
| `EphemeralInfra` | Starts real local infrastructure for the test, such as a database, queue, cache, object store, or service emulator. Containers are a common example, but not required. |
| `SharedEnv` | Uses a shared dev/test/staging environment. This should be rare for PR-gated tests. |
| `ThirdParty` | Calls a real external vendor or third-party sandbox. This should be isolated, explicitly tagged, and rarely required for PR validation. |

> **In-memory database providers (e.g. EF Core in-memory, SQLite-as-substitute) are `InMemory`, not `EphemeralInfra`.** They cannot validate provider-specific SQL, migrations, indexes, transactions, or concurrency. SQLite in-memory is `EphemeralInfra` only when SQLite is the production database.

---

# Code-Driven Test Types

## 1. Unit Test

A **unit test** verifies a small unit of behavior in isolation.

The unit can be a function, method, class, domain object, policy, parser, mapper, validator, or calculation. It does not need to map one-to-one with a class.

### Use when

- The behavior is local and deterministic.
- The logic has meaningful branching or edge cases.
- The behavior can be tested without framework/runtime wiring.
- The failure should point to a small area of code.

### Good targets

- Domain rules.
- Eligibility decisions.
- Pricing or scoring calculations.
- Validation logic.
- Mapping rules.
- Parsing and formatting.
- State transitions.
- Error classification.
- Date/time logic with a controllable clock.

### Avoid when

- The test mostly verifies a framework.
- The test duplicates implementation details.
- The test requires excessive mocks.
- The behavior is only meaningful through app wiring, persistence, serialization, HTTP, or UI rendering.

### Agent guidance

> Prefer unit tests for dense logic. Do not require a unit test for every class. A useful unit test verifies behavior that could actually break.

---

## 2. Component Test

A **component test** verifies a meaningful application or UI component without launching the full system.

A component may be a UI component, application service, handler, use case, state store, presenter, view model, or module-level collaboration.

### Use when

- A component has behavior worth verifying independently.
- The component has state, rendering, branching, validation, or orchestration logic.
- Dependencies can be replaced with simple fakes, stubs, or in-memory substitutes.
- The full application is not needed for confidence.

### Good targets

- UI component state and conditional rendering.
- Form validation at component level.
- Event handling.
- Application service behavior.
- Command/query handlers.
- Presenter/view-model behavior.
- State management logic.
- Module-level orchestration.

### Avoid when

- The component is a trivial wrapper.
- The behavior depends on full routing, authentication, browser layout, or real persistence.
- The test repeats lower-level unit coverage without additional confidence.

### Agent guidance

> Use component tests when a meaningful unit of user-facing or application-facing behavior can be verified without full system startup.

---

## 3. Slice Test

A **slice test** verifies a vertical or horizontal slice through multiple in-process layers while controlling external dependencies.

Examples include:

- Endpoint/controller → validation → handler → domain logic.
- Command/query handler → domain model → fake repository.
- UI component → state store → fake API client.
- Request pipeline → authorization rule → handler.
- Service module → mapper → policy → fake gateway.

### Use when

- The risk is collaboration between application layers.
- Framework wiring is partly relevant, but real infrastructure is not.
- A unit test would require too many mocks and lose meaning.
- You want confidence in orchestration, not just isolated logic.

### Good targets

- Request/response behavior below full HTTP.
- Application pipeline behavior.
- Authorization and validation flow.
- Handler orchestration.
- Mapping across boundaries.
- Use case behavior with fake persistence.

### Avoid when

- Real database/query behavior is the risk.
- Real HTTP serialization or routing is the risk.
- The test grows into a broad E2E scenario.
- Assertions span multiple unrelated outcomes.

### Agent guidance

> Use slice tests when behavior lives in the collaboration between components but can still be verified inside the codebase without real external dependencies.

---

## 4. In-Process Integration Test

An **in-process integration test** starts meaningful parts of the application inside the test process or local test host.

It may use a real dependency injection container, routing layer, middleware pipeline, serialization stack, validation stack, or local HTTP test server.

### Use when

- Application composition matters.
- Routing, serialization, middleware, filters, dependency injection, configuration, or framework behavior is part of the risk.
- You need to prove the application can boot with expected registrations.
- You need more confidence than a slice test but do not require real infrastructure.

### Good targets

- App startup.
- Dependency injection registration.
- Middleware behavior.
- Error handling shape.
- Request model binding.
- Response serialization.
- Auth policy wiring.
- Local HTTP request into an in-process app host.

### Avoid when

- The behavior is pure business logic.
- Real infrastructure behavior matters.
- The test is only being used to increase coverage.
- The suite becomes slow without adding confidence.

### Agent guidance

> Use in-process integration tests for app/framework composition. Do not use them as the default home for all business logic tests.

---

## 5. Ephemeral Infrastructure Integration Test

An **ephemeral infrastructure integration test** verifies code against real local infrastructure created and owned by the test suite.

Common examples:

- Local or containerized database.
- Local or containerized cache.
- Local or containerized queue.
- Local service emulator.
- Local object storage emulator.
- Temporary filesystem-backed service.
- Test-owned Docker Compose environment.
- Test-owned service process started by the harness.

### Use when

- The dependency's real behavior is part of the risk.
- In-memory or mocked behavior may lie.
- Persistence, query semantics, migrations, indexes, transactions, isolation levels, queue behavior, cache expiration, or provider-specific behavior matters.
- You need confidence that code interacts correctly with a real dependency.

### Good targets

- SQL query correctness.
- ORM provider behavior.
- Migrations.
- Database constraints.
- Transaction boundaries.
- Message publish/consume flow.
- Cache expiration.
- Search indexing/analyzers.
- Object storage behavior.
- Infrastructure adapter behavior.

### Avoid when

- A fake would provide equivalent confidence.
- The dependency behavior is irrelevant to the change.
- The test cannot isolate data.
- The dependency is shared and mutable.
- Startup cost would make the default suite painful.

### Database-specific rules

- Real database integration tests must use the production database engine, or its closest officially-supported equivalent.
- In-memory database providers and SQLite-as-substitute are `InMemory`, not `EphemeralInfra`. They cannot validate migrations, provider-specific SQL, indexes, constraints, transactions, isolation levels, or concurrency behavior.
- Each test must control its own seed data and cleanup. Tests must not depend on production data or shared mutable state.
- Use ephemeral infrastructure tests for: migrations, transaction behavior, SQL syntax, provider-specific queries, indexes and constraints, concurrency behavior, raw SQL, ORM mapping behavior.

### Agent guidance

> Use ephemeral infrastructure when dependency realism matters. Prefer test-owned infrastructure over shared dev databases or long-lived test environments.

---

## 6. Contract Test

A **contract test** verifies that a consumer and provider agree on the shape and semantics of a boundary.

Contract tests are code-driven and usually live in the consumer, provider, or both.

### Use when

- Services deploy independently.
- Different teams own the consumer and provider.
- Breaking API/message compatibility is a real risk.
- You need fast feedback before cross-service integration.
- Consumers have expectations that providers must preserve.

### Good targets

- HTTP request/response compatibility.
- Message schema compatibility.
- Required fields and optional fields.
- Error response shape.
- Backward-compatible provider changes.
- Client SDK/provider compatibility.

### Avoid when

- The boundary is internal to a single deployable.
- The same team owns both sides and integration tests already give enough confidence.
- You are testing business logic instead of compatibility.
- No provider verification step exists.

### Agent guidance

> Use contract tests for independently deployed boundaries. Contract tests do not replace provider tests or critical E2E smoke flows.

---

## 7. API / Service Boundary Test

An **API test** verifies a service through its public service boundary, usually HTTP or messaging, without requiring a browser.

The service may run in-process, as a local process, in ephemeral infrastructure, or in a controlled test environment.

### Use when

- The service contract matters.
- Routing, status codes, headers, auth, serialization, validation, or error payloads matter.
- The browser does not add useful confidence.
- You want to test behavior from the perspective of a client.

### Good targets

- Endpoint behavior.
- Status codes.
- Error payloads.
- Auth/authz enforcement.
- Request validation.
- Public API compatibility.
- Idempotency.
- Message handling.

### Avoid when

- A unit, component, or slice test would catch the same issue.
- The test simply duplicates UI E2E coverage.
- The test depends on fragile shared data.
- The browser is the actual risk.

### External-service rules

- Do not call live third-party services from normal PR test suites.
- Tests that hit shared external systems (vendor sandboxes, staging dependencies) must be tagged `SharedEnv` or `ThirdParty` and excluded from the default PR gate.
- Prefer fake servers, recorded fixtures, or contract tests for consumer-side behavior.

### Agent guidance

> Prefer API tests over UI E2E tests when the browser is not relevant to the behavior.

---

## 8. UI Component Test

A **UI component test** verifies a UI component using code-driven rendering and interaction, without a full end-to-end application flow.

This may run in a simulated DOM, framework test renderer, real browser component harness, or lightweight UI test host.

### Use when

- Rendering and user interaction matter.
- You need to verify UI state, conditional display, validation, emitted events, accessibility attributes, or component behavior.
- Full application navigation is not required.
- A real browser may or may not be necessary depending on the risk.

### Good targets

- Conditional rendering.
- Form validation.
- Button enabled/disabled states.
- Component events.
- State transitions.
- Accessibility roles/labels.
- Error messages.
- Loading/empty/success/error states.

### Avoid when

- The test depends on full routing, auth, backend integration, or cross-page behavior.
- The behavior is pure formatting better tested as a unit test.
- Assertions are brittle snapshots with unreadable diffs.
- CSS layout or browser-specific behavior requires a browser-level test instead.

### Agent guidance

> Use UI component tests for most UI state and rendering behavior. Do not push every UI branch into full browser E2E tests.

---

## 9. UI End-to-End Test

A **UI end-to-end test** drives a user journey through a browser or browser-equivalent automation tool using code-defined tests.

### Use when

- The browser is part of the risk.
- Routing, authentication, cookies, redirects, frontend/backend integration, client-side state, or real user flow matters.
- The flow is business-critical.
- A lower-level test cannot provide the same confidence.
- You need a smoke or regression test from the user's perspective.

### Good targets

- Login/logout.
- Critical form submission.
- Checkout or lead submission.
- Search/filter journey.
- Role-based UI behavior.
- Multi-page workflows.
- Critical client-side validation plus API integration.
- Smoke coverage for high-value pages.

### Avoid when

- You are testing every business-rule permutation.
- The same behavior can be verified through component or API tests.
- The test relies on arbitrary sleeps.
- The test depends on execution order.
- Selectors are brittle.
- Test data is hard to create or clean up.

### Agent guidance

> Use UI E2E tests sparingly for critical journeys and browser-specific risk. Most behavioral permutations belong below the browser layer.

---

## 10. Characterization Test

A **characterization test** captures existing behavior before changing legacy or poorly understood code.

It may assert behavior that is strange or undesirable if that behavior currently exists and must be preserved during refactor.

### Use when

- Refactoring legacy code.
- Modernizing a codebase.
- Replacing a dependency.
- Behavior is poorly documented.
- The goal is to preserve current behavior before intentional changes.
- You need a safety net before restructuring.

### Good targets

- Legacy service behavior.
- Unclear business rules.
- Complex conditionals.
- Existing API response shape.
- Serialization compatibility.
- Migration/refactor safety.

### Avoid when

- New intended behavior is already clear.
- The current behavior is known to be wrong and should change.
- The test would freeze a bug unintentionally.
- The captured output is too broad to review.

### Agent guidance

> Use characterization tests before risky refactors. Clearly label them so future maintainers know they preserve existing behavior, not necessarily ideal behavior.

---

## 11. Approval / Golden-Master Test

An **approval test** or **golden-master test** compares current output to an approved baseline.

The baseline may be a file, snapshot, serialized payload, rendered output, generated document, or structured response.

### Use when

- Output is large or hard to assert field-by-field.
- Human review of changes is valuable.
- Refactoring must preserve complex output.
- Generated content has a stable expected shape.
- Snapshot diffs are readable and actionable.

### Good targets

- Generated HTML.
- Generated documents.
- API payloads with stable structure.
- Report output.
- Configuration generation.
- Serialization output.
- Legacy behavior capture.

### Avoid when

- The baseline changes constantly.
- Diffs are too large to understand.
- The test approves implementation noise.
- The snapshot hides meaningful behavior.
- Reviewers routinely approve changes without inspection.

### Agent guidance

> Use approval tests when the output is easier to review as a whole than assert piece-by-piece. Keep approved artifacts readable and intentionally scoped. Do not update approved baselines merely to make tests pass — review them as behavior.

---

## 12. Property-Based Test

A **property-based test** verifies that an invariant holds across many generated inputs.

### Use when

- The behavior has clear invariants.
- Edge cases are easy to miss with example-based tests.
- Generated input can reveal meaningful failures.
- The result should satisfy general properties rather than one expected example.

### Good targets

- Parsers.
- Serializers/deserializers.
- Round-trip conversions.
- Date/time calculations.
- Sorting and ranking.
- Filtering.
- Eligibility rules.
- Pricing invariants.
- Idempotency.
- Monotonicity.
- Algebraic or state-machine behavior.

### Avoid when

- The invariant is unclear.
- Generated data is unrealistic or invalid.
- Failures would be hard to reproduce.
- The code is mostly framework glue.
- Shrunk counterexamples would not be useful.

### Agent guidance

> Use property-based tests for broad input confidence. Always make failures reproducible by preserving seeds or counterexamples.

---

## 13. Mutation Testing

**Mutation testing** changes production code in controlled ways and verifies that the test suite fails.

This is usually run by a mutation testing tool that invokes the normal repo test suite.

### Use when

- You need to evaluate test quality, not just coverage.
- The code is critical.
- The area has complex rules.
- Coverage is high but confidence is low.
- The team wants to harden shared libraries or core business logic.

### Good targets

- Domain rules.
- Pricing/scoring algorithms.
- Shared libraries.
- Security-sensitive decisions.
- Validation logic.
- High-risk refactored code.

### Avoid when

- The suite is unstable.
- Runtime cost is too high for frequent execution.
- The code is mostly framework glue.
- The results will not be acted on.
- The team is still building basic test coverage.

### Agent guidance

> Mutation testing is a strategy for measuring whether tests catch behavioral changes. Use it selectively; do not make it a universal PR requirement.

---

# Test Double Terminology

Use these terms consistently.

| Term | Meaning |
|---|---|
| Dummy | Passed only to satisfy an argument. Not used by the test. |
| Stub | Returns preconfigured values. |
| Fake | Working simplified implementation, often in-memory. |
| Spy | Records calls for later assertions. |
| Mock | Defines expectations about interactions. |
| Simulator | Higher-fidelity fake that approximates an external system. |

Guidelines:

- Prefer observable outcomes over internal interaction checks.
- Mock external boundaries, not every internal class.
- Do not mock the system under test.
- Use fakes for simple stateful behavior.
- Use ephemeral infrastructure when fake behavior would materially differ from production behavior.
- Interaction assertions are acceptable when the interaction itself is the behavior, such as publishing a message or calling a gateway adapter.

---

# Standard "It Depends" Rules

## Use the lowest layer that catches the bug

If a unit test catches the regression, do not add a UI E2E test for the same behavior.

## Move up a layer when lower layers hide the risk

Use a higher-level test when the lower-level test would mock away the thing that might break.

## Use real infrastructure only when realism matters

Use ephemeral infrastructure when dependency behavior is part of the risk. Otherwise prefer fakes or in-memory substitutes.

## Use browser tests only when browser/user-flow behavior matters

Do not test every business rule through the browser. Test most permutations below UI E2E.

## Use contract tests for independently deployed boundaries

Do not rely only on E2E tests to discover service compatibility breaks.

## Use characterization before refactor

When behavior is unclear but must be preserved, capture it before changing structure.

## Use approval tests for complex output

Do not write hundreds of fragile field assertions when a scoped, readable approved output would be clearer.

## Use property-based tests for invariants

When behavior should hold for many inputs, supplement examples with generated cases.

## Use mutation testing selectively

Use mutation testing to assess test strength in critical areas, not as a blanket metric.

---

# Required Test Metadata

Every test must be classifiable along these dimensions:

```text
Layer
Size
DependencyMode
Owner or Area (when meaningful)
```

The classification must be **discoverable at the suite level** — meaning a reviewer or CI configuration can determine the values for any test without reading the test body. The mechanism is the repo's choice.

## Acceptable mechanisms

Pick one (or combine when a single suite spans multiple modes):

- **Folder convention.** `tests/Unit/`, `tests/Integration.RealDb/`, etc. The path encodes the values; document the mapping in the project's `CLAUDE.md`. This is the lowest-overhead option and is the right default for most repos.
- **Test project / module separation.** `Repo.Tests.Unit`, `Repo.Tests.Integration`, etc. Same idea as folder convention but stronger — also enforces dependency boundaries.
- **Per-test attributes / traits / annotations / tags.** xUnit `[Trait]`, JUnit `@Tag`, Pytest markers, etc. Right tool when a single project legitimately mixes dependency modes (e.g., a few Testcontainers tests living next to in-process tests) and CI needs to filter at test-runner level.
- **Naming convention.** Test class or method name carries a suffix (`*IntegrationTests`, `*_E2E`). Acceptable when other mechanisms don't fit the test runner.
- **Build target grouping.** Maven/Gradle/MSBuild target separation. Often paired with project separation.

What matters is that **the classification exists somewhere a reviewer and CI can both consume**. Per-test attribute tagging is one valid option, not the default — do not require it when folder or project structure already does the job.

## Examples

A folder-convention repo (most common):

```text
tests/
  Unit/                       # Layer=Unit, Size=Small, DependencyMode=None
  Component/                  # Layer=Component, Size=Small, DependencyMode=TestDouble
  Integration.RealDb/         # Layer=Integration, Size=Medium, DependencyMode=EphemeralInfra
  E2E/                        # Layer=UIE2E, Size=Large, DependencyMode=SharedEnv
```

The project `CLAUDE.md` maps each folder to its triplet; new tests inherit the folder's classification automatically.

A per-test-attribute repo (mixed-mode suite):

```csharp
[Trait("Layer", "Integration")]
[Trait("Size", "Medium")]
[Trait("DependencyMode", "EphemeralInfra")]
[Trait("Owner", "Pricing")]
public sealed class PricingMigrationTests { ... }
```

Used when one suite intentionally mixes modes and `dotnet test --filter "DependencyMode=EphemeralInfra"` is a real workflow.

The mechanism is local to the technology stack. The vocabulary is enterprise-standard.

---

# Naming Standards

Test names should describe behavior, not implementation.

Recommended pattern:

```text
Given_Precondition_When_Action_Then_ExpectedOutcome
```

or natural-language equivalents supported by the test framework.

Good examples:

```text
Rejects expired offer when submitted after expiration date
Returns validation error when required customer email is missing
Publishes inventory update event after vehicle price changes
Renders empty state when no search results are returned
Preserves existing response shape during legacy refactor
```

Avoid:

```text
Test1
ShouldWork
HandlerTest
CallsRepository
SubmitButtonClick
```

---

# Folder and Suite Organization

Each repository should choose a consistent organization model.

Acceptable models include:

## By test layer

```text
tests/
  Unit/
  Component/
  Slice/
  Integration/
  Contract/
  UI/
```

## By product area

```text
tests/
  Inventory/
    Unit/
    Integration/
    Contract/
  Pricing/
    Unit/
    Integration/
```

## By technology convention

```text
src/
  FeatureA/
    FeatureA.Tests/
  FeatureB/
    FeatureB.Tests/
```

or

```text
packages/
  feature-a/
    __tests__/
  feature-b/
    __tests__/
```

Enterprise standard:

> The repo must make it obvious which tests are small, medium, large, and which tests require infrastructure.

---

# CI Placement

## PR Gate

Default PR gate should include:

```text
Small unit tests
Small component tests
Small slice tests
Fast in-process integration tests
Relevant contract tests
```

Allowed in PR gate when stable and reasonably fast:

```text
Medium ephemeral infrastructure tests
API/service boundary tests
Small browser smoke tests
```

Usually not required on every PR:

```text
Full UI E2E suite
Third-party sandbox tests
Shared environment tests
Mutation testing
Long-running approval suites
Large cross-service scenarios
```

## Main Branch / Merge Queue

Run:

```text
All PR-gated tests
Medium integration suites
Ephemeral infrastructure integration tests
Provider contract verification
Critical API tests
Critical UI smoke tests
```

## Nightly / Scheduled

Run:

```text
Full UI E2E
Large shared-environment suites
Third-party sandbox tests
Mutation testing for selected modules
Long-running property-based suites
Broad approval/golden-master suites
```

## Release Gate

Run:

```text
Deployment smoke tests
Critical API tests
Critical UI E2E journeys
Contract compatibility checks
High-risk integration suites
```

---

# Agentic AI Rules

When modifying code, the AI agent must:

1. Identify the changed behavior.
2. Identify the risk category.
3. Select the lowest-cost code-driven test that protects the behavior.
4. Prefer existing test patterns in the repository.
5. Add or update tests before declaring completion when practical.
6. Run the smallest relevant test command.
7. Report exactly which tests were run.
8. Never claim tests passed unless the command actually ran.
9. If tests cannot be run, report the blocker and provide the exact command a developer should run.
10. Avoid adding brittle tests purely to satisfy coverage.
11. Avoid creating tests that only assert mocks were called unless the interaction itself is the behavior.
12. Add a regression test for bug fixes whenever practical.
13. Use characterization or approval tests before risky refactors of unclear legacy behavior.
14. Prefer lower-level tests for permutations and higher-level tests for integration confidence.
15. **Do not weaken, delete, or rewrite existing assertions to make a change pass.** If an existing test appears wrong, isolate the test change from the behavioral change and explain why.
16. **Do not mix broad structural refactors with unrelated test rewrites in the same diff.**

---

# AI Test Selection Decision Tree

## Step 1: Is the behavior pure logic?

Use:

```text
Unit
Property-based
```

Consider mutation testing later if the logic is critical.

## Step 2: Is the behavior inside a component/module?

Use:

```text
Component
Slice
```

## Step 3: Is framework/app wiring part of the risk?

Use:

```text
In-process integration
API boundary test
```

## Step 4: Is real dependency behavior part of the risk?

Use:

```text
Ephemeral infrastructure integration
```

## Step 5: Is service compatibility across teams/deployables part of the risk?

Use:

```text
Contract test
```

## Step 6: Is the public service boundary the thing being verified?

Use:

```text
API / service boundary test
```

## Step 7: Is the browser or full user journey part of the risk?

Use:

```text
UI component test
UI end-to-end test
```

## Step 8: Is this a legacy refactor?

Use:

```text
Characterization test
Approval / golden-master test
```

## Step 9: Are we evaluating whether the tests are strong enough?

Use:

```text
Mutation testing
```

---

# Recommended Enterprise Defaults

## Most business logic changes

```text
Unit tests for rules
Component or slice tests for orchestration
API/integration tests only where boundary behavior matters
```

## UI changes

```text
UI component tests for state and rendering
UI E2E tests for critical flows only
```

## Data/persistence changes

```text
Unit tests for query-building or mapping logic when useful
Ephemeral infrastructure integration tests for real persistence behavior
Migration tests when schema changes
```

## Service boundary changes

```text
Unit/component tests for internal logic
Contract tests for compatibility
API tests for exposed provider behavior
```

## Legacy modernization

```text
Characterization tests before refactor
Approval tests for complex output
Unit/slice tests after behavior is understood
Mutation testing selectively after stabilization
```

## Bug fixes

```text
Regression test at the lowest layer that would have caught the bug
Higher-level test only when the lower layer cannot reproduce the failure
```

---

# Anti-Patterns

Avoid:

```text
Requiring unit tests for every class
Using line coverage as the main quality signal
Testing private methods directly
Mocking every dependency by default
Asserting implementation instead of behavior
Adding UI E2E tests for every acceptance criterion
Using shared mutable QA data in PR tests
Depending on test execution order
Using arbitrary sleeps in async/browser tests
Creating snapshots with unreadable diffs
Approving golden-master changes without review
Adding tests that pass even when the original bug exists
Claiming tests passed when they were not run
Calling in-memory or SQLite-substitute tests "integration" tests against the real database
```

---

# Per-Project CLAUDE.md Requirements

Each project's `CLAUDE.md` must define its actual commands and conventions. The vocabulary above is enterprise-wide; the realization is per-repo.

Required command entries (omit any that don't apply, but document why):

- Build
- Typecheck (if applicable)
- Lint / static analysis
- Unit tests
- Component / module tests
- Slice tests (if separated)
- Contract tests (if applicable)
- Real-dependency / ephemeral-infrastructure integration tests (if applicable)
- API / service-boundary tests (if separated)
- UI component tests (if applicable)
- UI E2E tests (if applicable)
- Mutation tests (if applicable)

Each project must also document:

- Test framework conventions (xUnit, Jest, Pytest, etc.).
- Test naming, tagging, and folder conventions.
- Whether in-memory or fake-backed providers are used and how they are classified (`InMemory` vs `EphemeralInfra`).
- How real external dependencies are started locally (containers, emulators, scripts).
- Which test suites are expected to run for normal PR-sized changes.
- Any deliberate carve-outs from this enterprise standard, with rationale.

---

# Enterprise Standard Summary

The organization standardizes:

```text
Test vocabulary
Layer definitions
Size definitions
Dependency modes
Tagging expectations
CI placement expectations
Agentic AI decision rules
```

Each repository owns:

```text
Specific test framework
Exact folder structure
Exact commands
Technology-specific fixtures
Local test data strategy
Stack-specific helper libraries
```

Final rule:

> A code-driven test is valuable when it protects meaningful behavior, runs reliably, fails clearly, and is placed at the lowest layer that provides the needed confidence.
