# Contributing

Contributions are welcome. This is a solo-maintained project (DrMurloc), so the process is lightweight — but the review bar is real. Read the policies below before you open a PR; they are enforced liberally, not aspirationally.

## The flow

1. Branch from `main`.
2. Open a PR. A descriptive title and a few sentences on what changed and why.
3. Ping **DrMurloc** in [Discord](https://discord.gg/AvS5PxnvSN) when it's ready for review.

## Contribution policies

### 1. AI contributions are allowed — cohesion is not optional

AI-assisted and AI-generated contributions are welcome. However, **large-scale contributions will be declined liberally if the cohesive target isn't immediately understandable.** If I have to reverse-engineer what your PR is *for*, it's getting closed, not studied.

### 2. Human in the loop is a must

I expect AI contributors to be using AI to **explore and understand** their changes — not to single-prompt a diff and ship it. If I even suspect a contributor doesn't understand the code they changed, I will decline the PR liberally. AI automates low-level decisions; it does not replace human decision making. You are the author of record: be able to explain every line, why it's there, and what breaks without it.

### 3. One PR, one actionable

Pull requests must be well scoped to a **single actionable**: a single bug fix, a single refactor, a single feature add. A small amount of scope creep (a low-hanging-fruit bug fix you tripped over, for instance) is acceptable, but it **must be called out explicitly** in a PR comment by the contributor — undeclared drive-by changes read as noise and count against the PR.

### 4. Testing is mandatory

Untested code is highly subject to declined PRs. See [HOW-TO-TEST.md](HOW-TO-TEST.md) for the test taxonomy and where a change of your shape should be tested. The default expectation: a regression test at the lowest layer that would have caught the bug, or behavioral coverage for the feature you added.

### 5. Talk to me *before* large refactors

Architectural changes and large refactors must be communicated to me **before** the PR exists. I will scrutinize structural changes, and I expect you to be conversant in **DDD, Onion, Hexagonal, and Medallion** terminology before we talk shop on large refactors. Unannounced architecture PRs get closed regardless of quality. [ARCHITECTURE.md](ARCHITECTURE.md) describes the philosophy the codebase already follows — start there.

## Build and test

```sh
dotnet build ScoreTracker/ScoreTracker.sln -c Release
dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj          # unit/component — no Docker
dotnet test ScoreTracker/ScoreTracker.Tests.Api/ScoreTracker.Tests.Api.csproj  # API wire-shape approval
```

Run these locally before opening a PR. The real-database integration suite ([HOW-TO-TEST.md](HOW-TO-TEST.md)) needs Docker. CI runs on [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker); merges to `main` additionally go through an approval-gated production deploy.

## Getting a working environment

[HOW-TO-RUN.md](HOW-TO-RUN.md) — prerequisites, one-command local run via Aspire, and the dev-harness page that populates your local database from the live site.

## Conventions

- [ARCHITECTURE.md](ARCHITECTURE.md) — architecture philosophy and the code map. Read before any non-trivial PR.
- [CLAUDE.md](CLAUDE.md) — machine-readable conventions for AI coding agents (layer package allowlists, test patterns, coverage exclusions). If you're contributing with an AI agent, point it here.
- [DOMAIN.md](DOMAIN.md) — Pump It Up domain glossary (Mix, Chart, Phoenix score, Pumbility, UCS, …).

## Community

- Discord: <https://discord.gg/AvS5PxnvSN> — where changes get discussed and contributors stay in sync.
- [Code of Conduct](CODE_OF_CONDUCT.md) · [Security policy](SECURITY.md)
