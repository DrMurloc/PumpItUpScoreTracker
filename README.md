# PumpItUpScoreTracker

A community-run web app for tracking [Pump It Up](https://en.wikipedia.org/wiki/Pump_It_Up_(video_game_series)) scores, leaderboards, tournaments, and player progression.

**Live site:** https://piuscores.arroweclip.se/

## What it does

- Per-player score tracking across the Phoenix and XX mixes
- Chart and song catalog with difficulty / tier-list browsing
- Leaderboards — world, regional, and community-scoped
- Tournament hosting — brackets, score submission with photo verification, approval workflow
- Player rating ("Pumbility") and progression analytics
- Weekly charts and UCS (User-Created Step) tracking
- Discord bot for community notifications

New to Pump It Up? See [DOMAIN.md](DOMAIN.md) for the terms used here.

## Use it

Open https://piuscores.arroweclip.se/ — the live deployment is the recommended way to use the tracker.

Self-hosting is possible but currently requires SQL Server plus OAuth (Discord / Google / Facebook), SendGrid, and Azure Blob credentials. A friction-free local-dev mode is on the [roadmap](BACKLOG.md).

## Contribute

Contributions are welcome.

```sh
dotnet build ScoreTracker/ScoreTracker.sln -c Release
dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj
```

- **[CONTRIBUTING.md](CONTRIBUTING.md)** — the flow: branch, PR, ping DrMurloc in Discord
- **[ARCHITECTURE.md](ARCHITECTURE.md)** — solution layout, layer rules, eventing, data access
- **[BACKLOG.md](BACKLOG.md)** — areas where help is most welcome
- **CI** — [Azure Pipelines](https://dev.azure.com/joneccker/ScoreTracker) builds and tests on every push to `main`
- **Discord** — <https://discord.gg/AvS5PxnvSN>

## Architecture at a glance

ASP.NET Core 10 hosting both Blazor Server (UI) and MVC API controllers, on an onion-architecture core: a pure `Domain` layer, a MediatR-based `Application` layer, an EF Core / SQL Server `Infrastructure` layer, and DI wired through a `CompositionRoot`. Asynchronous and recurring work runs on MassTransit with an in-memory transport. Full detail in [ARCHITECTURE.md](ARCHITECTURE.md).

## License

[MIT](LICENSE) &copy; 2022 DrMurloc
