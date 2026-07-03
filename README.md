# PumpItUpScoreTracker

A community-run web app for tracking [Pump It Up](https://en.wikipedia.org/wiki/Pump_It_Up_(video_game_series)) scores, leaderboards, tournaments, and player progression.

**Live site:** https://piuscores.arroweclip.se/

## What it does

- Per-player score tracking with full submission history
- Chart and song catalog with difficulty and tier-list browsing
- Leaderboards — world, official-mirror, and community-scoped
- Tournament hosting — brackets, qualifiers, score submission with photo verification
- Player rating ("Pumbility") and progression analytics
- Weekly challenge charts and UCS (User-Created Step) leaderboards
- Discord bot for community notifications
- A token-authenticated [API](docs/API.md) for community tool makers

New to Pump It Up? [DOMAIN.md](docs/DOMAIN.md) defines the terms used throughout.

## Run it locally

One command, once [Git, the .NET 10 SDK, and Docker Desktop](docs/HOW-TO-RUN.md#prerequisites) are installed:

```sh
git clone https://github.com/DrMurloc/PumpItUpScoreTracker.git
cd PumpItUpScoreTracker
dotnet run --project ScoreTracker/ScoreTracker.AppHost
```

That provisions a local SQL Server container, applies migrations, and opens the app — then a guided setup page populates your database with real chart data from the live site. No credentials required. Full walkthrough: **[HOW-TO-RUN.md](docs/HOW-TO-RUN.md)**.

## Documentation

| Doc | What's in it |
|---|---|
| [HOW-TO-RUN.md](docs/HOW-TO-RUN.md) | Prerequisites, local setup, the dev harness, optional configuration |
| [HOW-TO-TEST.md](docs/HOW-TO-TEST.md) | Testing philosophy and how to run each suite |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Architecture philosophy (bounded-context verticals, DDD + onion + hexagonal) and the code map |
| [DATABASE-SCHEMA.md](docs/DATABASE-SCHEMA.md) | Every table, grouped by owning vertical |
| [API.md](docs/API.md) | The API surface at a glance; Swagger is the source of truth |
| [SCHEDULED-JOBS.md](docs/SCHEDULED-JOBS.md) | The Hangfire recurring jobs: what they do and when |
| [TECHNOLOGIES.md](docs/TECHNOLOGIES.md) | The stack: what each technology contributes and how it's integrated |
| [CONTRIBUTING.md](docs/CONTRIBUTING.md) | Contribution policies — read before opening a PR |
| [DOMAIN.md](docs/DOMAIN.md) | Pump It Up domain glossary |

## Contribute

Contributions are welcome — including AI-assisted ones, with a human firmly in the loop. Read **[CONTRIBUTING.md](docs/CONTRIBUTING.md)** first; the policies there are enforced.

- **CI**: [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker) — build + all test suites on every PR, approval-gated deploy from `main`
- **Discord**: <https://discord.gg/AvS5PxnvSN> — where changes get discussed

**Building a PIU tool?** Don't build your own importer — the tracker exposes token-authenticated APIs and score-import webhooks. See [API.md](docs/API.md) and ask on Discord.

## License

[MIT](LICENSE) &copy; 2022 DrMurloc
