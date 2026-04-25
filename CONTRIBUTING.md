# Contributing

Contributions are welcome. The flow is intentionally minimal — this is a one-person project today.

## The flow

1. Branch from `main`.
2. Open a PR. A descriptive title and a sentence or two on what changed and why is enough — no template required.
3. Ping **DrMurloc** in [Discord](https://discord.gg/AvS5PxnvSN) when it's ready for review.

That's it.

## Build and test

```sh
dotnet build ScoreTracker/ScoreTracker.sln -c Release
dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj
```

Run these locally before opening a PR. Fork PRs don't trigger CI today — see [BACKLOG.md](BACKLOG.md) (*Fork-PR CI feedback*) for the deferred plan. CI runs on [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker) after merge to `main`.

## What you need locally

- **Domain / Application / test changes** — just the .NET 10 SDK. No database or credentials required to build and run the test suite. This is the lowest-friction lane and a great place for a first PR.
- **Web / runtime changes** — also SQL Server (LocalDB or Docker), plus OAuth, SendGrid, and Azure Blob credentials depending on which features you touch. Friction is real today; see [BACKLOG.md](BACKLOG.md) (*Phase 1 — Local dev without prod credentials*) for the docker-compose + null-adapter roadmap.

Configuration goes via user-secrets or `appsettings.Development.json`. Database migrations are applied manually with the EF Core tooling — there is no startup auto-migrate.

## Project conventions

[**CLAUDE.md**](CLAUDE.md) and [**ARCHITECTURE.md**](ARCHITECTURE.md) are the primary architectural directives. Read them before a non-trivial PR.

- **CLAUDE.md** — layer package allowlists, test patterns, the `[ExcludeFromCodeCoverage]` convention for new commands/queries/events, and project-specific carve-outs.
- **ARCHITECTURE.md** — solution layout, dependency graph, eventing model, data access, and a domain glossary (Mix, Phoenix score, Pumbility, UCS, Bounty, Saga, etc.).

For where help is most welcome, see [BACKLOG.md](BACKLOG.md).

## Community

Discord is where changes get posted and contributors stay in sync: <https://discord.gg/AvS5PxnvSN>
